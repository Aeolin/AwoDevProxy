using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace AwoDevProxy.Web.Api.Proxy
{
	public interface IProxyManager
	{
		public Task<bool> IsProxyAvailableAsync(string id);
		public Task<bool> HandleProxyAsync(HttpContext context);
		public bool RequiresAuthentication(HttpContext context, out string password, out string authHeader, out byte[] fingerPrint);

		public Task<IActionResult> SetupProxy(string name, WebSocket socket, TimeSpan timeout, string password = null, string authHeaderName = null);
	}
}
