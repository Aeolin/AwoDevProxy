using AwoDevProxy.Web.Api.Proxy;
using Microsoft.AspNetCore.Http.Extensions;
using System.Collections.Frozen;
using System.Net.NetworkInformation;

namespace AwoDevProxy.Web.Api.Middleware
{
	public class ProxyRootingMiddleware
	{
		private readonly IProxyManager _proxy;
		private readonly RequestDelegate _next;
		private readonly ProxyConfig _config;
		private readonly FrozenSet<string> _domains;
		private readonly ILogger _logger;

		public ProxyRootingMiddleware(IProxyManager proxy, RequestDelegate next, ProxyConfig config, ILoggerFactory factory)
		{
			_proxy=proxy;
			_next=next;
			_config=config;
			_domains = config.Domains.Select(x => x.ToLower()).ToFrozenSet();
			_logger = factory.CreateLogger<ProxyRootingMiddleware>();
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var host = context.Request.Host.Host;
			var split = host.IndexOf('.');
			if (split > 0)
			{
				var key = host.Substring(0, split);
				var domain = host.Substring(split+1);
				if (_domains.Contains(domain.ToLower()) && await _proxy.IsProxyAvailableAsync(key))
				{
					var data = new ProxyRequestData(key);
					context.AddProxyData(data);
					context.TraceIdentifier = data.LogValue;
					_logger.LogInformation("Rooted Request[{requestId}] for {method} {url}", data.LogValue, context.Request.Method, context.Request.GetDisplayUrl());	
				}
			}

			if (context.Response.HasStarted == false)
				await _next.Invoke(context);
		}
	}
}
