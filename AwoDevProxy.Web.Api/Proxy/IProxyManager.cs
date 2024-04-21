using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace AwoDevProxy.Web.Api.Proxy
{
	public interface IProxyManager
	{
		public Task<bool> IsProxyAvailableAsync(string id);
		public Task<bool> HandleProxyAsync(string id, HttpContext context);
		public Task<IActionResult> SetupProxy(string name, WebSocket socket, TimeSpan timeout, string password = null);
	}
}
