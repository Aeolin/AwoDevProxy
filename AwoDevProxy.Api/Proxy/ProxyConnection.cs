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
		private readonly byte[] _buffer;
		private readonly CancellationTokenSource _tokenSource;
		private readonly MemoryStream _currentPacket;
		private readonly Dictionary<Guid, TaskCompletionSource<ProxyResponseModel>> _openRequests = new Dictionary<Guid, TaskCompletionSource<ProxyResponseModel>>();

		public event Action<ProxyConnection> SocketClosed;
		public event Action<ProxyConnection, byte[]> PacketReceived;

		private void HandlePacketReceived()
		{
			var data = _currentPacket.ToArray();
			_currentPacket.SetLength(0);
			PacketReceived?.Invoke(this, data);
			var response = MessagePackSerializer.Deserialize<ProxyResponseModel>(data);
			if(_openRequests.TryGetValue(response.RequestId, out var tcs))
			{
				tcs.TrySetResult(response);
				_openRequests.Remove(response.RequestId);
			}
		}

		public async Task<ProxyResponseModel> SendRequestAsync(ProxyRequestModel model, int? timeout = 5000)
		{
			var data = MessagePackSerializer.Serialize(model);
			var tcs = new TaskCompletionSource<ProxyResponseModel>();
			try
			{
				_openRequests.Add(model.RequestId, tcs);
				await Socket.SendAsync(data, WebSocketMessageType.Binary, true, _tokenSource.Token);
				if (_tokenSource.IsCancellationRequested)
					tcs.SetCanceled();

				if (timeout.HasValue)
				{
					await Task.WhenAny(tcs.Task, Task.Delay(timeout.Value));
					if (tcs.Task.IsCompleted)
						return tcs.Task.Result;

					return null;
				}
				else
				{
					return await tcs.Task;
				}
			}
			finally
			{
				_openRequests.Remove(model.RequestId);
			}
		}

		public async Task<IActionResult> SocketWaitLoop()
		{
			while (Socket.State.HasFlag(WebSocketState.Open) && _tokenSource.IsCancellationRequested == false)
			{
				try
				{
					var received = await Socket.ReceiveAsync(_buffer, _tokenSource.Token);
					await _currentPacket.WriteAsync(_buffer, 0, received.Count);
					if (received.EndOfMessage)
						HandlePacketReceived();
				}
				catch (IOException)
				{
					break;
				}
				catch (TaskCanceledException)
				{
					break;
				}
				catch (Exception)
				{
					await Socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Unexpected exception occured", CancellationToken.None);
					throw;
				}
			}

			if (Socket.State.HasFlag(WebSocketState.Open))
				await Socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Connection closed", CancellationToken.None);

			Dispose();
			return new StatusCodeResult(200);
		}

		public void Close()
		{
			if (_tokenSource.IsCancellationRequested == false)
				_tokenSource.Cancel();
		}

		public void Dispose()
		{
			Socket.Dispose();
			_currentPacket.Dispose();
			SocketClosed?.Invoke(this);
		}

		public ProxyConnection(string path, WebSocket socket, int bufferSize = 2048)
		{
			Name = path;
			Socket = socket;
			_buffer = new byte[bufferSize];
			_tokenSource = new CancellationTokenSource();
			SocketTask = SocketWaitLoop();
			_currentPacket = new MemoryStream();
		}


	}
}
