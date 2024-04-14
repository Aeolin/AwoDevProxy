using AwoDevProxy.Api.Proxy;
using Microsoft.AspNetCore.Mvc;

namespace AwoDevProxy.Api.Controllers
{
	public class ProxyController : ControllerBase
	{
		private ProxyManager _manager;
		private ProxyConfig _config;

		public ProxyController(ProxyManager manager, ProxyConfig config)
		{
			_manager=manager;
			_config=config;
		}

		[Route("/ws/{name}")]
		public async Task<IActionResult> SetupProxyAsync(string name, [FromQuery]bool force = false, [FromQuery]string authKey = null)
		{
			if (HttpContext.WebSockets.IsWebSocketRequest == false)
				return BadRequest();

			if (_config.FixedKey != null && _config.FixedKey.Equals(authKey) == false)
				return Forbid();

			if (force == false && _manager.ProxyExists(name))
				return BadRequest("Proxy already exists for the give name");

			var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
			var proxy = await _manager.SetupProxyAsync(name, socket);
			return await proxy.SocketTask;
		}
	}
}
