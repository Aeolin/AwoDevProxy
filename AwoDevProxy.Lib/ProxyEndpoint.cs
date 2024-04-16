using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AwoDevProxy.Lib
{
	public class ProxyEndpoint
	{
		private readonly HttpClient _http;
		public ProxyEndpointConfig Config { get; init; }
		private CancellationTokenSource _cancelToken;
		private RecyclableMemoryStreamManager _streamManager;
		private Dictionary<Guid, WebSocketProxy> _webSocketProxies;
		private ClientWebSocket _webSocket;
		private readonly byte[] _buffer;
		private readonly MemoryStream _currentPacket;
		private const int RECONNECT_DELAY = 5;
		private const int MAX_RECCONECT_DELAY = 60;
		private List<Task> _tasks;
		private ILogger _logger;

		public ProxyEndpoint(ProxyEndpointConfig config, ILoggerFactory factory = null, RecyclableMemoryStreamManager manager = null)
		{
			Config = config;
			_http = new HttpClient() { BaseAddress = new Uri(config.LocalAddress) };
			_buffer = new byte[config.BufferSize];
			_currentPacket = new MemoryStream();
			_logger = factory?.CreateLogger<ProxyEndpoint>();
			_streamManager=manager ?? new RecyclableMemoryStreamManager();
			_webSocketProxies = new Dictionary<Guid, WebSocketProxy>();
			_tasks = new List<Task>();
		}


		private async Task HandlePacketAsync(Stream stream)
		{
			var packet = PacketSerializer.Deserialize<MessageType>(stream, out var type);
			object result = null;
			switch (type)
			{
				case MessageType.HttpRequest:
					result = await Handle_HttpRequest_Async((ProxyHttpRequest)packet);
					break;

				case MessageType.WebSocketOpen:
					result = await Handle_WebSocketOpen_Async((ProxyWebSocketOpen)packet);
					break;

				case MessageType.WebSocketData:
					await Handle_WebSocketData_Async((ProxyWebSocketData)packet);
					break;

				case MessageType.WebSocketClose:
					await Handle_WebSocketClose_Async((ProxyWebSocketClose)packet);
					break;
			}

			if (result != null)
				await SendPacketAsync(result);
		}



		private async Task SendPacketAsync(object packet)
		{
			using var mem = _streamManager.GetStream();
			PacketSerializer.Serialize<MessageType>(packet, (IBufferWriter<byte>)mem);
			await _webSocket.SendAsync(mem.GetReadOnlySequence(), _cancelToken.Token);
		}

		private async Task Handle_WebSocketClose_Async(ProxyWebSocketClose request)
		{
			if (_webSocketProxies.TryGetValue(request.SocketId, out var proxy))
			{
				await proxy.CloseAsync();
				_webSocketProxies.Remove(proxy.Id);
			}
		}

		private async Task Handle_WebSocketData_Async(ProxyWebSocketData request)
		{
			if (_webSocketProxies.TryGetValue(request.SocketId, out var proxy))
			{
				if (request.MessageType == WebSocketMessageType.Close)
				{
					await proxy.CloseAsync();
					_webSocketProxies.Remove(proxy.Id);
				}
				else
				{
					await proxy.SendAsync(request);
				}
			}
		}

		private async Task<ProxyWebSocketOpenAck> Handle_WebSocketOpen_Async(ProxyWebSocketOpen request)
		{
			var client = new ClientWebSocket();
			var url = $"{Config.LocalAddress}/{request.PathAndQuery}";
			var index = url.IndexOf("://");
			url = $"{request.Protocol}{url.Substring(index)}";
			var cts = new CancellationTokenSource();

			try
			{
				await client.ConnectAsync(new Uri(url), cts.Token);
				var proxy = new WebSocketProxy(request.SocketId, client, cts);
				_webSocketProxies.Add(request.SocketId, proxy);
				_tasks.Add(proxy.ReadAsync());
				_logger.LogInformation($"Established websocket proxy with id[{request.SocketId}] for {request.PathAndQuery}");
				return new ProxyWebSocketOpenAck { SocketId = request.SocketId, Success = true };
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "error opening websocket proxy");
				return new ProxyWebSocketOpenAck { SocketId = request.SocketId, Success = false, ResponseCode = 500, ErrorMessage = ex.Message };
			}
		}

		private async Task<ProxyHttpResponse> Handle_HttpRequest_Async(ProxyHttpRequest request)
		{
			try
			{
				var httpRequest = ProxyUtils.CreateRequestFromProxy(request);
				var response = await _http.SendAsync(httpRequest);
				var proxyResponse = await ProxyUtils.CreateResponseFromHttpAsync(response, request.RequestId);
				_logger?.LogInformation($"Handeled request for {request.PathAndQuery}, response: {response.StatusCode}");
				return proxyResponse;
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, $"Error handling Request[{request.RequestId}] for {request.PathAndQuery}");
				return ProxyUtils.CreateResponseFromError(500, ex.ToString(), request.RequestId);
			}
		}

		private async Task<bool> TryRunAsync(Uri uri)
		{
			bool connected = false;
			try
			{
				_webSocket = new ClientWebSocket();
				await _webSocket.ConnectAsync(uri, _cancelToken.Token);
				connected = true;
				_logger?.LogInformation("Connected to ProxyServer");
				_tasks.Add(_webSocket.ReceiveAsync(_buffer, _cancelToken.Token));
				while (_webSocket.State.HasFlag(WebSocketState.Open) && _cancelToken.IsCancellationRequested == false)
				{
					var any = await Task.WhenAny(_tasks);
					_tasks.Remove(any);
					if (any is Task<WebSocketReceiveResult> readTask)
					{
						var received = readTask.Result; //await _webSocket.ReceiveAsync(_buffer, _cancelToken.Token);
						if (received.MessageType == WebSocketMessageType.Close)
							return true;

						_currentPacket.Write(_buffer, 0, received.Count);
						if (received.EndOfMessage)
						{
							try
							{
								_currentPacket.Position = 0;
								await HandlePacketAsync(_currentPacket);
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, "Error handling packet");
							}
							_currentPacket.SetLength(0);
						}
					}
					else if (any is Task<ProxyWebSocketData> dataTask)
					{
						await SendPacketAsync(dataTask.Result);
						if (_webSocketProxies.TryGetValue(dataTask.Result.SocketId, out var socket))
							_tasks.Add(socket.ReadAsync());
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				if (_cancelToken.IsCancellationRequested == false)
					_logger?.LogError(ex, "Unexpected exception occured in receive loop");


				return connected;
			}
		}

		public async Task DisposeAsync()
		{
			if (_webSocket != null && _webSocket.State.HasFlag(WebSocketState.Open))
				await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

			foreach (var proxy in _webSocketProxies.Values)
				await proxy.CloseAsync();

			_webSocketProxies.Clear();
			_tasks.Clear();
		}

		public async Task RunAsync(CancellationTokenSource cts)
		{
			_cancelToken = cts ?? new CancellationTokenSource();
			var uri = new Uri($"{Config.ProxyServer}/ws/{Config.Name}?authKey={HttpUtility.UrlEncode(Config.AuthKey)}");
			int retryCount = 0;
			do
			{
				var result = await TryRunAsync(uri);
				if (_cancelToken.IsCancellationRequested)
				{
					_logger?.LogInformation("Shutting down...");
					await DisposeAsync();
					return;
				}

				if (result)
				{
					retryCount = 0;
					_logger?.LogInformation($"Disconnected, attempting to recconect in {RECONNECT_DELAY} seconds");
					await Task.Delay(TimeSpan.FromSeconds(RECONNECT_DELAY));
				}
				else
				{
					retryCount++;
					var delay = Math.Min(MAX_RECCONECT_DELAY, retryCount*RECONNECT_DELAY);
					_logger?.LogInformation($"Reconnection attempt failed, trying again in {delay} seconds");
					await Task.Delay(TimeSpan.FromSeconds(delay));
				}

			} while (_cancelToken.IsCancellationRequested == false && Config.TryReopen);

		}
	}
}
