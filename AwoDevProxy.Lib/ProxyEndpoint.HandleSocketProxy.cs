using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AwoDevProxy.Lib
{
	public partial class ProxyEndpoint
	{
		private Dictionary<Guid, WebSocketProxy> _webSocketProxies;

		private async Task CloseProxyAsync(WebSocketProxy proxy, bool sendClosedMessage = true)
		{
			if (_webSocketProxies.ContainsKey(proxy.Id))
			{
				await proxy.CloseAsync();
				_webSocketProxies.Remove(proxy.Id);
				if (sendClosedMessage)
				{
					var message = new ProxyWebSocketClose { SocketId = proxy.Id };
					await SendPacketAsync(message);
				}

				_logger?.LogInformation($"WebSocketProxy[{proxy.Id}] closed");
			}
		}

		private Task<WebSocketProxyReadResult> GetTask_WebSocket(WebSocketProxy proxy) => proxy.ReadAsync();

		private async Task<bool> Handle_WebSocket_ReadResultAsync(WebSocketProxy proxy, WebSocketProxyReadResult result)
		{
			if (result.IsOpen)
			{
				var data = result.DataFrame;
				await SendPacketAsync(data);
				_logger?.LogInformation($"Received {data.Data.Count} bytes from WebSocketProxy[{proxy.Id}]");
				return true;
			}
			else
			{
				await CloseProxyAsync(proxy);
				return false;
			}
		}

		private async Task Handle_WebSocket_CloseAsync(ProxyWebSocketClose request)
		{
			if (_webSocketProxies.TryGetValue(request.SocketId, out var proxy))
				await CloseProxyAsync(proxy, false);
		}

		private async Task Handle_WebSocket_DataAsync(ProxyWebSocketData request)
		{
			if (_webSocketProxies.TryGetValue(request.SocketId, out var proxy))
			{
				if (request.MessageType == WebSocketMessageType.Close)
				{
					await CloseProxyAsync(proxy);
				}
				else
				{
					await proxy.SendAsync(request);
					_logger.LogInformation($"Sent {request.Data.Count} bytes to WebSocketProxy[{proxy.Id}]");
				}
			}
		}

		private async Task<ProxyWebSocketOpenAck> Handle_WebSocket_OpenAsync(ProxyWebSocketOpen request)
		{
			var client = new ClientWebSocket();
			var url = $"{Config.LocalAddress}{request.PathAndQuery}";
			var index = url.IndexOf("://");
			url = $"{Config.WebSocketScheme}{url.Substring(index)}";
			var cts = new CancellationTokenSource();

			try
			{
				//if (request.Headers != null)
				//	foreach (var header in ProxyConstants.FilterHeaders(request.Headers))
				//		if( header.Key != "Sec-WebSocket-Key")
				//			client.Options.SetRequestHeader(header.Key, header.Value.First());

				await client.ConnectAsync(new Uri(url), cts.Token);
				var proxy = new WebSocketProxy(request.SocketId, client, cts);
				_webSocketProxies.Add(request.SocketId, proxy);
				_taskManager.SubmitSource(proxy);
				_logger.LogInformation($"Established websocket proxy with id[{request.SocketId}] for {request.PathAndQuery}");
				return new ProxyWebSocketOpenAck { SocketId = request.SocketId, Success = true };
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "error opening websocket proxy");
				return new ProxyWebSocketOpenAck { SocketId = request.SocketId, Success = false, ResponseCode = 500, ErrorMessage = ex.Message };
			}
		}


	}
}
