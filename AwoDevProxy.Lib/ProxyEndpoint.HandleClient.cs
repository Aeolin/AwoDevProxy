using AwoDevProxy.Shared.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Lib
{
	public partial class ProxyEndpoint
	{
		private readonly byte[] _buffer;
		private RecyclableMemoryStream _packetBuffer;

		private Task<WebSocketReceiveResult> GetTask_Client(ClientWebSocket socket)
		{
			return socket.ReceiveAsync(_buffer, _cancelToken.Token);
		}

		private bool Handle_Client_TaskCancelledException(ClientWebSocket socket, TaskCanceledException ex)
		{
			return false;
		}

		private async Task<bool> Handle_Client_ReadResultAsync(ClientWebSocket socket, WebSocketReceiveResult result)
		{
			if (result.MessageType == WebSocketMessageType.Close)
			{
				await DisposeAsync();
				return false;
			}

			if (_packetBuffer == null)
				_packetBuffer = _streamManager.GetStream();

			await _packetBuffer.WriteAsync(_buffer);
			if (result.EndOfMessage)
			{
				try
				{
					_packetBuffer.Position = 0;
					await HandlePacketAsync(_packetBuffer);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error handling packet");
				}
				await _packetBuffer.DisposeAsync();
				_packetBuffer = null;
			}

			return true;
		}

		private async Task<ProxyHttpResponse> Handle_Client_HttpRequestAsync(ProxyHttpRequest request)
		{
			try
			{
				var httpRequest = ProxyUtils.CreateRequestFromProxy(request);
				var response = await _http.SendAsync(httpRequest);
				var proxyResponse = await ProxyUtils.CreateResponseFromHttpAsync(response, request.RequestId);
				_logger?.LogInformation($"Handeled request[{request.RequestId}] for {request.PathAndQuery}, response: {response.StatusCode}");
				return proxyResponse;
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, $"Error handling Request[{request.RequestId}] for {request.PathAndQuery}");
				return ProxyUtils.CreateResponseFromError(500, ex.ToString(), request.RequestId);
			}
		}
	}
}
