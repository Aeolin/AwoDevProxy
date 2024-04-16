using AwoDevProxy.Api.Utils;
using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IO;
using System.Buffers;
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
		private readonly TimedTaskHolder<Guid, ProxyHttpResponse> _openRequests;
		private readonly RecyclableMemoryStreamManager _streamManager;

		public event Action<ProxyConnection> SocketClosed;


		private void HandlePacketReceived(Stream stream)
		{
			var data = PacketSerializer.Deserialize<MessageType>(stream, out var key);
			switch (key)
			{
				case MessageType.HttpResponse:
					var response = (ProxyHttpResponse)data;
					_openRequests.SetResult(response.RequestId, response);
					break;

			}
		}

		public async Task<ProxyResult> HandleHttpRequestAsync(ProxyHttpRequest model)
		{
			var stream = _streamManager.GetStream();
			PacketSerializer.Serialize<MessageType>(model, (IBufferWriter<byte>)stream);
			var task = _openRequests.GetTask(model.RequestId, _timeout);
			await Socket.SendAsync(stream.GetReadOnlySequence(), _cancelSource.Token);
			await stream.DisposeAsync();
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
					if (received.MessageType == WebSocketMessageType.Close)
						break;
					

					await packetBuffer.WriteAsync(buffer, 0, received.Count);
					if (received.EndOfMessage)
					{
						packetBuffer.Position = 0;
						HandlePacketReceived(packetBuffer);
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

		public ProxyConnection(RecyclableMemoryStreamManager streamPool, string name, WebSocket socket, TimeSpan requestTimeout, int bufferSize = 2048)
		{
			Name = name;
			Socket = socket;
			_bufferSize = bufferSize;
			_cancelSource = new CancellationTokenSource();
			SocketTask = SocketWaitLoop();
			_timeout = requestTimeout;
			_openRequests = new TimedTaskHolder<Guid, ProxyHttpResponse>();
			_streamManager = streamPool;
		}


	}
}
