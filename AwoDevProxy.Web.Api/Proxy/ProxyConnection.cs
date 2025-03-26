using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using AwoDevProxy.Shared.Utils;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IO;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyConnection : IDisposable
	{
		public string Name { get; init; }

		public WebSocket Socket { get; init; }
		public Task<IActionResult> SocketTask { get; init; }
		public byte[] AuthFingerprint { get; init; }

		private int _bufferSize;
		private readonly CancellationTokenSource _cancelSource;
		private readonly TimeSpan _timeout;

		private readonly ConcurrentDictionary<Guid, ProxyConnectionRequestBase> _requests;

		private readonly TimedTaskHolder<Guid, ProxyHttpResponse> _openRequests;
		private readonly TimedTaskHolder<Guid, ProxyWebSocketOpenAck> _openWebsockets;
		private readonly ConcurrentDictionary<Guid, WebSocketProxy> _webSocketProxies;
		private readonly ConcurrentDictionary<Guid, Stream> _openResponseStreams;

		private readonly RecyclableMemoryStreamManager _streamManager;
		private readonly SemaphoreSlim _webSocketLock = new SemaphoreSlim(1, 1);

		public event Action<ProxyConnection> SocketClosed;

		private readonly ILogger _logger;
		public string Password { get; init; }
		public string AuthHeaderScheme { get; init; }


		public ProxyConnection(RecyclableMemoryStreamManager streamPool, string name, WebSocket socket, TimeSpan requestTimeout, ILoggerFactory factory, string password = null, string authHeaderScheme = null, int bufferSize = 4096)
		{
			Name = name;
			Socket = socket;
			_bufferSize = bufferSize;
			_cancelSource = new CancellationTokenSource();
			_timeout = requestTimeout;
			_openRequests = new TimedTaskHolder<Guid, ProxyHttpResponse>();
			_openWebsockets = new TimedTaskHolder<Guid, ProxyWebSocketOpenAck>();
			_streamManager = streamPool;
			_logger = factory.CreateLogger($"{nameof(ProxyConnection)}[{name}]");
			SocketTask = SocketWaitLoop();
			Password = password;
			AuthHeaderScheme = authHeaderScheme;
			AuthFingerprint = password == null ? null : MD5.HashData(Encoding.UTF8.GetBytes($"{name}#{password}"));
		}



		private async Task HandleDataFrameAsync(ProxyDataFrame frame, Action<ProxyConnectionRequestBase> action = null)
		{
			if (_requests.TryGetValue(frame.RequestId, out var request))
			{
				action?.Invoke(request);
				var done = await request.WriteAsync(frame);
				if (done)
					_requests.Remove(frame.RequestId, out _);
			}
		}

		private async void HandlePacketReceived(Stream stream)
		{
			var packet = PacketSerializer.Deserialize<MessageType>(stream, out var key);
			switch (key)
			{
				case MessageType.HttpResponse:
					var response = (ProxyHttpResponse)packet;
					await HandleDataFrameAsync(response.Body, req =>
					{
						if (req is ProxyConnectionHttpRequest http)
							http.BeginResponse(response);
					});
					break;


				case MessageType.DataFrame:
					var dataFrame = (ProxyDataFrame)packet;
					await HandleDataFrameAsync(dataFrame);
					break;

				case MessageType.WebSocketOpenAck:
					var open = (ProxyWebSocketOpenAck)packet;
					_openWebsockets.SetResult(open.SocketId, open);
					_logger.LogDebug("Got packet[{packetType}] from client for websocket[{requestId}] open", key, open.SocketId);
					break;
			}
		}

		private async Task SendPacketAsync(object packet)
		{
			var stream = _streamManager.GetStream();
			var key = PacketSerializer.Serialize<MessageType>(packet, (IBufferWriter<byte>)stream);
			_logger.LogDebug("Sent packet[{packetType}] to client with {dataAmount} bytes", key, stream.Length);
			await _webSocketLock.WaitAsync();

			try
			{
				await Socket.SendAsync(stream.GetReadOnlySequence(), _cancelSource.Token);
			}
			finally
			{
				_webSocketLock.Release();
			}

			await stream.DisposeAsync();
		}

		private async Task CloseWebSocketProxyAsync(Guid proxyId, bool notifyClient)
		{
			if (_webSocketProxies.Remove(proxyId, out var proxy))
			{
				if (notifyClient)
					await SendPacketAsync(new ProxyWebSocketClose { SocketId = proxy.Id });

				await proxy.CloseAsync();
				_logger.LogDebug("Closed WebSocketProxy[{id}]", proxyId);
			}
		}

		public async Task HandleWebSocketProxyAsync(WebSocketProxy proxy)
		{
			_webSocketProxies.TryAdd(proxy.Id, proxy);
			WebSocketProxyReadResult read;
			while ((read = await proxy.ReadAsync()).IsOpen)
				await SendPacketAsync(read.DataFrame);

			await CloseWebSocketProxyAsync(proxy.Id, true);
		}

		public async Task<WebSocketResult> OpenWebSocketProxyAsync(ProxyWebSocketOpen model)
		{
			var task = _openWebsockets.GetTask(model.SocketId, _timeout);
			await SendPacketAsync(model);
			var result = await task;

			if (result.TimedOut)
			{
				await SendPacketAsync(new ProxyWebSocketClose { SocketId = model.SocketId });
				return WebSocketResult.FromError(500, "Request Timed out");
			}

			return WebSocketResult.FromResponse(result.Result);
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
			RecyclableMemoryStream packetBuffer = null;
			var buffer = new byte[_bufferSize];

			try
			{
				while (Socket.State.HasFlag(WebSocketState.Open) && _cancelSource.IsCancellationRequested == false)
				{
					var received = await Socket.ReceiveAsync(buffer, _cancelSource.Token);
					if (received.MessageType == WebSocketMessageType.Close)
						break;

					if (packetBuffer == null)
						packetBuffer = _streamManager.GetStream();

					//_logger.LogTrace("Received websocket data: {dataAmount} bytes, end: {endOfMessage}", received.Count, received.EndOfMessage);
					await packetBuffer.WriteAsync(buffer, 0, received.Count);
					if (received.EndOfMessage)
					{
						_logger.LogDebug("Received websocket message containing {dataAmount} bytes", packetBuffer.Length);
						packetBuffer.Position = 0;
						HandlePacketReceived(packetBuffer);
						await packetBuffer.DisposeAsync();
						packetBuffer = null;
					}
				}
			}
			catch (Exception ex)
			{
				if (ex is not TaskCanceledException && ex is not WebSocketException)
					throw;
			}
			finally
			{
				packetBuffer?.Dispose();

				if (Socket.State == WebSocketState.Open)
					await Socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Unexpected exception occured", CancellationToken.None);

				Dispose();
			}

			return new StatusCodeResult(200);
		}

		public void Close()
		{
			if (_cancelSource.IsCancellationRequested == false)
				_cancelSource.Cancel();

			Dispose();
		}

		public void Dispose()
		{
			_openRequests.Dispose();
			_openWebsockets.Dispose();
			Socket.Dispose();
			SocketClosed?.Invoke(this);
		}
	}
}
