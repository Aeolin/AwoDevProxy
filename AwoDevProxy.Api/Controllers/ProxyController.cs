using AwoDevProxy.Api.Proxy;
using Microsoft.AspNetCore.Mvc;

namespace AwoDevProxy.Api.Controllers
{
	public class ProxyController : ControllerBase
	{
		private IProxyManager _manager;
		private ProxyConfig _config;

		public ProxyController(IProxyManager manager, ProxyConfig config)
		{
			_manager=manager;
			_config=config;
		}

		[Route("/ws/{name}")]
		[ApiExplorerSettings(IgnoreApi = true)]
		public async Task<IActionResult> SetupProxyAsync(string name, [FromQuery]bool force = false, [FromQuery]string authKey = null, [FromQuery]TimeSpan? requestTimeout = null)
		{
			if (HttpContext.WebSockets.IsWebSocketRequest == false)
				return BadRequest();

			if (_config.FixedKey != null && _config.FixedKey.Equals(authKey) == false)
				return StatusCode(StatusCodes.Status403Forbidden, "wrong key");

			var exists = await _manager.IsProxyAvailableAsync(name);
			if (force == false && exists)
				return BadRequest("Proxy already exists for the give name");

			var timeout = requestTimeout ?? _config.DefaultTimeout;
			if (_config.MaxTimeout < timeout)
				timeout = _config.MaxTimeout;

			var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
			var proxyTask = _manager.SetupProxy(name, socket, timeout);
			return await proxyTask;
		}
	}
}
