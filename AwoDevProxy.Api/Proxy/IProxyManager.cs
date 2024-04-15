using Microsoft.AspNetCore.Mvc;
using System.Formats.Asn1;
using System.Net.WebSockets;

namespace AwoDevProxy.Api.Proxy
{
	public interface IProxyManager
	{
		public Task<bool> IsProxyAvailableAsync(string id);
		public Task<bool> HandleProxyAsync(string id, HttpContext context);
		public Task<IActionResult> SetupProxy(string name, WebSocket socket, TimeSpan timeout);
	}
}
