using AwoDevProxy.Api.Proxy;
using Microsoft.AspNetCore.Mvc;

namespace AwoDevProxy.Api.Controllers
{
	public class ProxyController : ControllerBase
	{
		private IProxyManager _manager;
		private ProxyConfig _config;
		private ILogger _logger;

		public ProxyController(IProxyManager manager, ProxyConfig config, ILoggerFactory factor)
		{
			_manager=manager;
			_config=config;
			_logger = factor.CreateLogger<ProxyController>();
		}

		[Route("/ws")]
		[ApiExplorerSettings(IgnoreApi = true)]
		public async Task<IActionResult> SetupProxyAsync([FromQuery]string name, [FromQuery]bool force = false, [FromQuery]string authKey = null, [FromQuery]TimeSpan? requestTimeout = null, [FromQuery]string password = null)
		{
			if (HttpContext.WebSockets.IsWebSocketRequest == false)
			{
				_logger.LogInformation("Declined /ws requesnt since it wasn't a websocket request");
				return BadRequest("Expected WebSocket Environment");
			}

			if (_config.FixedKey != null && _config.FixedKey.Equals(authKey) == false)
			{
				_logger.LogInformation("Declined /ws request because keys didnt match");
				return StatusCode(StatusCodes.Status403Forbidden, "wrong key");
			}

			var exists = await _manager.IsProxyAvailableAsync(name);
			if (force == false && exists)
			{
				_logger.LogInformation($"Declined /ws request because proxy with name {name} already existed");
				return BadRequest("Proxy already exists for the give name");
			}

			var timeout = requestTimeout ?? _config.DefaultTimeout;
			if (_config.MaxTimeout < timeout)
				timeout = _config.MaxTimeout;

			var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
			var proxyTask = _manager.SetupProxy(name, socket, timeout, password);
			_logger.LogInformation($"Accepted /ws request for name {name}");
			await proxyTask;
			return default;
		}
	}
}
