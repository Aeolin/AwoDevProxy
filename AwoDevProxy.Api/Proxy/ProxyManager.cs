using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using MessagePack;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IO;
using System.Net.WebSockets;
using System.Text;

namespace AwoDevProxy.Api.Proxy
{
	public class ProxyManager : IProxyManager
	{
		private readonly Dictionary<string, ProxyConnection> _connections = new Dictionary<string, ProxyConnection>();
		private readonly ProxyConfig _config;
		private readonly RecyclableMemoryStreamManager _streamManager;

		public ProxyManager(ProxyConfig config, RecyclableMemoryStreamManager streamManager)
		{
			_config=config;
			_streamManager=streamManager;
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
		}

		private void AddProxy(ProxyConnection connection)
		{
			connection.SocketClosed += Connection_SocketClosed;
			_connections.Add(connection.Name, connection);
		}

		private void Connection_SocketClosed(ProxyConnection connection)
		{
			RemoveProxy(connection);
		}

		public Task<IActionResult> SetupProxy(string proxyName, WebSocket socket, TimeSpan requestTimeout)
		{
			proxyName = proxyName.ToLower();
			if (_connections.TryGetValue(proxyName, out ProxyConnection proxyConnection))
			{
				proxyConnection.Close();
				_connections.Remove(proxyName);
			}

			proxyConnection = new ProxyConnection(_streamManager, proxyName, socket, requestTimeout);
			AddProxy(proxyConnection);
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

		public async Task<bool> HandleProxyAsync(string id, HttpContext context)
		{
			if (_connections.TryGetValue(id, out var connection))
			{
				if (context.WebSockets.IsWebSocketRequest)
				{
					var request = new ProxyWebSocketOpen { PathAndQuery = context.Request.GetEncodedPathAndQuery(), Protocol = context.Request.Scheme };
					var result = await connection.OpenWebSocketProxyAsync(request);
					if (result.Success)
					{
						var socket = await context.WebSockets.AcceptWebSocketAsync();
						var proxy = new WebSocketProxy(request.SocketId, socket);
						await connection.HandleWebSocketProxyAsync(proxy);
						await ProxyUtils.WriteResultAsync(200, context.Response);
					}
					else
					{
						await ProxyUtils.WriteErrorAsync(result.Error, context.Response);
					}
				}
				else
				{
					var request = await ProxyUtils.ConstructProxyRequestAsync(context.Request);
					var result = await connection.HandleHttpRequestAsync(request);
					if (result.Success)
					{
						await ProxyUtils.WriteResponseToPipelineAsync(result.Response, context.Response);
					}
					else
					{
						await ProxyUtils.WriteErrorAsync(result.Error, context.Response);
					}
				}

				return true;
			}

			return false;
		}
	}
}
