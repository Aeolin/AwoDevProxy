using AwoDevProxy.Web.Api.Proxy;

namespace AwoDevProxy.Web.Api.Middleware
{
	public class ProxyHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger _logger;
		private readonly IProxyManager _proxyManager;

		public ProxyHandlingMiddleware(RequestDelegate next, ILoggerFactory factory, IProxyManager proxyManager)
		{
			_next=next;
			_logger=factory.CreateLogger<ProxyHandlingMiddleware>();
			_proxyManager=proxyManager;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var data = context.GetProxyData();
			if (data != null && await _proxyManager.HandleProxyAsync(context))
			{
				_logger.LogInformation("Handeled Request[{requestId}]: {statusCode}", data.LogValue, context.Response.StatusCode);
			}
			else
			{
				await _next.Invoke(context);
			}
		}
	}
}
