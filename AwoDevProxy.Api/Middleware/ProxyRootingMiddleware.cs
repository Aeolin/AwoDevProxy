using AwoDevProxy.Api.Proxy;

namespace AwoDevProxy.Api.Middleware
{
	public class ProxyRootingMiddleware
	{
		private readonly IProxyManager _proxy;
		private readonly RequestDelegate _next;
		private readonly ProxyConfig _config;

		public ProxyRootingMiddleware(IProxyManager proxy, RequestDelegate next, ProxyConfig config)
		{
			_proxy=proxy;
			_next=next;
			_config=config;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var host = context.Request.Host.Host;
			var subdomains = host.Split(".");
			if(subdomains.Length == _config.SubdomainLevel + 1)
			{
				var id = subdomains.First();
				if(await _proxy.IsProxyAvailableAsync(id))
				{
					if(await _proxy.HandleProxyAsync(id, context))
						return;
				}
			}

			await _next.Invoke(context);
		}
	}
}
