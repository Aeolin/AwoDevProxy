using AwoDevProxy.Api.Proxy;
using System.Collections.Frozen;
using System.Net.NetworkInformation;

namespace AwoDevProxy.Api.Middleware
{
	public class ProxyRootingMiddleware
	{
		private readonly IProxyManager _proxy;
		private readonly RequestDelegate _next;
		private readonly ProxyConfig _config;
		private readonly FrozenSet<string> _domains;

		public ProxyRootingMiddleware(IProxyManager proxy, RequestDelegate next, ProxyConfig config)
		{
			_proxy=proxy;
			_next=next;
			_config=config;
			_domains = config.Domains.Select(x => x.ToLower()).ToFrozenSet();
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
					if (await _proxy.HandleProxyAsync(key, context))
						return;
				}
			}

			await _next.Invoke(context);
		}
	}
}
