using AwoDevProxy.Api.Utils;
using AwoDevProxy.Shared;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace AwoDevProxy.Api.Proxy
{
	public class ProxyConnection : IDisposable
	{
		public string Name { get; init; }
		public WebSocket Socket { get; init; }
		public Task<IActionResult> SocketTask { get; init; }
		private int _bufferSize;
		private readonly CancellationTokenSource _cancelSource;
		private readonly TimeSpan _timeout;
		private readonly TimedTaskHolder<Guid, ProxyResponseModel> _openRequests;

		public event Action<ProxyConnection> SocketClosed;

		private void HandlePacketReceived(byte[] packet)
		{
			var response = MessagePackSerializer.Deserialize<ProxyResponseModel>(packet);
			_openRequests.SetResult(response.RequestId, response);
		}

		public async Task<ProxyResult> HandleRequestAsync(ProxyRequestModel model)
		{
			var data = MessagePackSerializer.Serialize(model);
			var task = _openRequests.GetTask(model.RequestId, _timeout);
			await Socket.SendAsync(data, WebSocketMessageType.Binary, true, _cancelSource.Token);
			var result = await task;
			if (result.TimedOut)
				return ProxyResult.FromError(500, "Request Timed out");

			return ProxyResult.FromResponse(result.Result);
		}

		public async Task<IActionResult> SocketWaitLoop()
		{
			var packetBuffer = new MemoryStream();
			var buffer = new byte[_bufferSize];
			
			try
			{
				while (Socket.State.HasFlag(WebSocketState.Open) && _cancelSource.IsCancellationRequested == false)
				{
					var received = await Socket.ReceiveAsync(buffer, _cancelSource.Token);
					await packetBuffer.WriteAsync(buffer, 0, received.Count);
					if (received.EndOfMessage)
					{
						HandlePacketReceived(packetBuffer.ToArray());
						packetBuffer.SetLength(0);
					}
				}
			}
			catch (Exception ex)
			{
				if (ex is not TaskCanceledException)
					throw;	
			}
			finally
			{
				if (Socket.State == WebSocketState.Open)
					await Socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Unexpected exception occured", CancellationToken.None);

				packetBuffer.Dispose();
			}

			Dispose();
			return new StatusCodeResult(200);
		}

		public void Close()
		{
			if (_cancelSource.IsCancellationRequested == false)
				_cancelSource.Cancel();
		}

		public void Dispose()
		{
			Socket.Dispose();
			SocketClosed?.Invoke(this);
		}

		public ProxyConnection(string path, WebSocket socket, TimeSpan requestTimeout, int bufferSize = 2048)
		{
			Name = path;
			Socket = socket;
			_bufferSize = bufferSize;
			_cancelSource = new CancellationTokenSource();
			SocketTask = SocketWaitLoop();
			_timeout = requestTimeout;
			_openRequests = new TimedTaskHolder<Guid, ProxyResponseModel>();
		}


	}
}
