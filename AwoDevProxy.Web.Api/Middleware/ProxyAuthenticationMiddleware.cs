using AwoDevProxy.Web.Api.Proxy;
using AwoDevProxy.Web.Api.Service.Cookies;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using System.Runtime.InteropServices;

namespace AwoDevProxy.Web.Api.Middleware
{
	public class ProxyAuthenticationMiddleware
	{
		private RequestDelegate _next;
		private IProxyManager _manager;
		private ICookieService _cookieService;
		private ILogger _logger;
		private ProxyConfig _config;

		public ProxyAuthenticationMiddleware(RequestDelegate next, ILoggerFactory factory, IProxyManager manager, ProxyConfig config, ICookieService cookieService)
		{
			_next = next;
			_manager = manager;
			_logger = factory.CreateLogger<ProxyAuthenticationMiddleware>();
			_config=config;
			_cookieService=cookieService;
		}

		public bool IsQueryAuthenticated(HttpContext context, string password) => context.Request.Query.TryGetValue(_config.AuthParamName, out var key) && password.Equals(key.First()) == true;
		public bool IsCookieAuthenticated(HttpContext context, byte[] fingerPrint) => context.Request.Cookies.TryGetValue(_config.AuthParamName, out var authCookie) && _cookieService.IsValid(authCookie, fingerPrint);
		public bool IsHeaderAuthenticated(HttpContext context, string password)
		{
			if (context.Request.Headers.TryGetValue(_config.AuthParamName, out var keys))
			{
				return password == keys.First();
			}
			else
			{
				if (context.Request.Headers.TryGetValue("Authorization", out var authorization) && authorization.First().StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
					return password == authorization.First().Substring(6).Trim();
			}

			return false;
		}


		public async Task InvokeAsync(HttpContext context)
		{
			var data = context.GetProxyData();
			if (data != null && _manager.RequiresAuthentication(context, out var password, out var fingerPrint))
			{
				if (IsHeaderAuthenticated(context, password))
				{
					await _next.Invoke(context);
					_logger.LogInformation("Header Authentication passed for Request[{requestId}]", data.LogValue);
					return;
				}

				if (context.Request.HasFormContentType && context.Request.Form.TryGetValue(_config.AuthParamName, out var key) && password.Equals(key.First()))
				{
					var cookie = _cookieService.CreateCookie(fingerPrint);
					context.Response.Cookies.Delete(_config.AuthParamName);
					context.Response.Cookies.Append(_config.AuthParamName, cookie);
					await ProxyUtils.WriteRedirectAsync(context.Response, context.Request.GetEncodedUrl(), StatusCodes.Status303SeeOther);
					_logger.LogInformation("Created Authentication Cookie for Request[{requestId}]", data.LogValue);
					return;
				}

				if (IsCookieAuthenticated(context, fingerPrint) == false && IsQueryAuthenticated(context, password) == false)
				{
					var content = File.ReadAllText("wwwroot/Login.html").Replace("{AuthParamName}", _config.AuthParamName);
					await ProxyUtils.WriteErrorAsync(StatusCodes.Status401Unauthorized, content, context.Response);
					_logger.LogInformation("Access denied for Request[{requestId}], returned login page", data.LogValue);
					return;
				}
			}

			await _next.Invoke(context);
		}

	}
}
