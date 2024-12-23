﻿using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using MessagePack;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyManager : IProxyManager
	{
		private readonly Dictionary<string, ProxyConnection> _connections = new Dictionary<string, ProxyConnection>();
		private readonly ProxyConfig _config;
		private readonly RecyclableMemoryStreamManager _streamManager;
		private readonly ILogger _logger;
		private readonly ILoggerFactory _factory;

		public ProxyManager(ProxyConfig config, RecyclableMemoryStreamManager streamManager, ILoggerFactory factory)
		{
			_config=config;
			_streamManager=streamManager;
			_logger = factory.CreateLogger<ProxyManager>();
			_factory = factory;
		}

		public bool ProxyExists(string name)
		{
			name = name.ToLower();
			if (_connections.TryGetValue(name, out ProxyConnection proxyConnection))
				return proxyConnection.Socket.State == WebSocketState.Open;

			return false;
		}

		private void RemoveProxy(ProxyConnection connection)
		{
			connection.SocketClosed -= Connection_SocketClosed;
			_connections.Remove(connection.Name);
			_logger.LogInformation("Proxy listener for subdomain {subdomain} removed", connection.Name);
		}

		private void AddProxy(ProxyConnection connection)
		{
			connection.SocketClosed += Connection_SocketClosed;
			_connections.Add(connection.Name, connection);
			_logger.LogInformation("Proxy listener for subdomain {subdomain} added", connection.Name);
		}

		private void Connection_SocketClosed(ProxyConnection connection)
		{
			RemoveProxy(connection);
		}

		public Task<IActionResult> SetupProxy(string proxyName, WebSocket socket, TimeSpan requestTimeout, string password = null, string authHeaderName = null)
		{
			proxyName = proxyName.ToLower();
			if (_connections.TryGetValue(proxyName, out ProxyConnection proxyConnection))
			{
				proxyConnection.Close();
				_connections.Remove(proxyName);
			}

			proxyConnection = new ProxyConnection(_streamManager, proxyName, socket, requestTimeout, _factory, password, authHeaderName, 4096*4);
			AddProxy(proxyConnection);
			_logger.LogInformation("New Proxy listener for subdomain {subdomain} create with timeout {timeout}", proxyName, requestTimeout);
			return proxyConnection.SocketTask;
		}

		private bool HealthCheck(ProxyConnection connection)
		{
			if (connection.Socket.State == WebSocketState.Closed)
			{
				connection.Close();
				return false;
			}

			return connection.Socket.State == WebSocketState.Open;
		}

		public Task<bool> IsProxyAvailableAsync(string id)
		{
			if (_connections.TryGetValue(id, out var connection))
				return Task.FromResult(HealthCheck(connection));

			return Task.FromResult(false);
		}

		public bool RequiresAuthentication(HttpContext context, out string password, out string authScheme, out byte[] fingerPrint)
		{
			var data = context.GetProxyData();
			if (data != null && _connections.TryGetValue(data.ProxySubdomain, out var connection))
			{
				password = connection.Password;
				authScheme = connection.AuthHeaderScheme ?? _config.DefaultAuthScheme;
				fingerPrint = connection.AuthFingerprint;
				return password != null;
			}

			password = null;
			fingerPrint = null;
			authScheme = null;
			return false;
		}

		public async Task<bool> HandleProxyAsync(HttpContext context)
		{
			var data = context.GetProxyData();
			if (data != null && _connections.TryGetValue(data.ProxySubdomain, out var connection))
			{
				if (context.WebSockets.IsWebSocketRequest)
				{
					_logger.LogInformation("Got websocket request[{requestId}] for path [{subdomain}:{path}]", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery());
					var request = ProxyUtils.ConstructWebSocketOpenRequest(context.Request, data.RequestId);
					var result = await connection.OpenWebSocketProxyAsync(request);
					if (result.Success && result.Response.Success)
					{
						var socket = await context.WebSockets.AcceptWebSocketAsync();
						var proxy = new WebSocketProxy(request.SocketId, socket);
						_logger.LogInformation("Accepted websocket request[{requestId}] for path [{subdomain}:{path}]", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery());
						await connection.HandleWebSocketProxyAsync(proxy);
					}
					else if (result.Success && result.Response.Success == false)
					{
						_logger.LogInformation("Rejected websocket request[{requestId}] to path [{subdomain}:{path}] because the cliend declined it", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery());
						await ProxyUtils.WriteErrorAsync(result.Response.ResponseCode, result.Response.ErrorMessage, context.Response);
					}
					else
					{
						_logger.LogInformation("Rejected websocket request[{requestId}] to path [{subdomain}:{path}] because of time out", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery());
						await ProxyUtils.WriteErrorAsync(result.Error, context.Response);
					}
				}
				else
				{
					_logger.LogInformation("Got http request[{requestId}] for path [{subdomain}:{path}]", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery());
					var request = await ProxyUtils.ConstructProxyRequestAsync(context.Request, data);
					var result = await connection.HandleHttpRequestAsync(request);
					if (result.Success)
					{
						await ProxyUtils.WriteResponseToPipelineAsync(result.Response, context.Response);
						_logger.LogInformation("Answered http request[{requestId}] to path [{subdomain}:{path}], result success: {status}", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery(), result.Response.StatusCode);
					}
					else
					{
						await ProxyUtils.WriteErrorAsync(result.Error, context.Response);
						_logger.LogInformation("Answered http request[{requestId}] to path [{subdomain}:{path}], result error: {status} {message}", data.LogValue, data.ProxySubdomain, context.Request.GetEncodedPathAndQuery(), result.Error.StatusCode, result.Error.Message);
					}
				}
			}
			else
			{
				await ProxyUtils.WriteErrorAsync(StatusCodes.Status401Unauthorized, "Include a devprxy-auth query or cookie", context.Response);
			}

			return false;
		}


	}
}
