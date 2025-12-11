using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;
using AwoDevProxy.Shared.Utils.Tasks;
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
	public partial class ProxyEndpoint
	{
		private readonly HttpClient _http;
		public ProxyEndpointConfig Config { get; init; }
		private CancellationTokenSource _cancelToken;
		private RecyclableMemoryStreamManager _streamManager;
		private TaskManager _taskManager;
		private ClientWebSocket _webSocket;
		private ILogger _logger;
		private SemaphoreSlim _webSocketLock = new SemaphoreSlim(1, 1);

		private const int RECONNECT_DELAY = 5;
		private const int MAX_RECCONECT_DELAY = 60;

		public ProxyEndpoint(ProxyEndpointConfig config, ILoggerFactory factory = null, RecyclableMemoryStreamManager manager = null)
		{
			Config = config;

			if (config.AllowLocalUntrustedCerts)
			{
				var handler = new HttpClientHandler();
				handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
				_http = new HttpClient(handler);
			}
			else
			{
				_http = new HttpClient();
			}

			_http.BaseAddress = new Uri(config.LocalAddress);
			if (config.RequestTimeout.HasValue)
				_http.Timeout = config.RequestTimeout.Value;

			_buffer = new byte[config.BufferSize];
			_logger = factory?.CreateLogger<ProxyEndpoint>();
			_streamManager=manager ?? new RecyclableMemoryStreamManager();
			_webSocketProxies = new Dictionary<Guid, WebSocketProxy>();
			_taskManager = new TaskManager(factory);
			SetupTasks(_taskManager);
		}

		private void SetupTasks(TaskManager manager)
		{
			manager.WithWakupAfterInactivity(TimeSpan.FromSeconds(30));
			manager.WithTaskSource<ClientWebSocket, WebSocketReceiveResult>(GetTask_Client, opts =>
			{
				opts.HandleResult(Handle_Client_ReadResultAsync);
				opts.HandleException<TaskCanceledException>(Handle_Client_TaskCancelledException);
			});

			manager.WithTaskSource<WebSocketProxy, WebSocketProxyReadResult>(GetTask_WebSocket, opts =>
			{
				opts.HandleResult(Handle_WebSocket_ReadResultAsync);
			});

			manager.WithTaskSource<ProxyHttpRequest, ProxyHttpResponse>(Handle_Client_HttpRequestAsync, opts =>
			{
				opts.HandleResult(HandlePacketResultAsync);
			});

			manager.WithTaskSource<ProxyWebSocketOpen, ProxyWebSocketOpenAck>(Handle_WebSocket_OpenAsync, opts =>
			{
				opts.HandleResult(HandlePacketResultAsync);
			});
		}


		private async Task HandlePacketAsync(Stream stream)
		{
			var packet = PacketSerializer.Deserialize<MessageType>(stream, out var type);
			switch (type)
			{
				case MessageType.HttpRequest:
				case MessageType.WebSocketOpen:
					_taskManager.SubmitSource(packet.GetType(), packet);
					break;

				case MessageType.WebSocketData:
					await Handle_WebSocket_DataAsync((ProxyWebSocketData)packet);
					break;

				case MessageType.WebSocketClose:
					await Handle_WebSocket_CloseAsync((ProxyWebSocketClose)packet);
					break;
			}
		}

		private async Task<bool> HandlePacketResultAsync<TRequest, TResponse>(TRequest resquest, TResponse response)
		{
			if (resquest != null)
				await SendPacketAsync(response);

			return false;
		}


		private async Task SendPacketAsync(object packet)
		{
			var mem = _streamManager.GetStream();
			PacketSerializer.Serialize<MessageType>(packet, (IBufferWriter<byte>)mem);
			await _webSocketLock.WaitAsync();
			await _webSocket.SendAsync(mem.GetReadOnlySequence(), _cancelToken.Token);
			_webSocketLock.Release();
			await mem.DisposeAsync();
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
				_taskManager.SubmitSource(_webSocket);
				while (await _taskManager.AwaitNextTask()) ;
				return true;
			}
			catch (TaskCanceledException)
			{
				return connected;
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
			_taskManager.Stop();
			if (_webSocket != null && _webSocket.State.HasFlag(WebSocketState.Open))
				await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

			foreach (var proxy in _webSocketProxies.Values)
				await proxy.CloseAsync();

			_webSocketProxies.Clear();
		}


		private string BuildQurey()
		{
			Dictionary<string, string> queryValues = new Dictionary<string, string>
			{
				{ "authKey", Config.AuthKey },
				{ "bufferSize", Config.BufferSize.ToString() },
				{ "name", Config.Name },
			};

			if (string.IsNullOrEmpty(Config.AuthHeaderScheme) == false)
				queryValues.Add("authHeaderScheme", Config.AuthHeaderScheme);

			if (Config.RequestTimeout.HasValue)
				queryValues.Add("requestTimeout", Config.RequestTimeout.Value.ToString());

			if (string.IsNullOrEmpty(Config.Password) == false)
				queryValues.Add("password", Config.Password);

			if (Config.ForceOpen.HasValue)
				queryValues.Add("force", Config.ForceOpen.Value.ToString());

			return string.Join("&", queryValues.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}"));
		}


		public async Task RunAsync(CancellationTokenSource cts)
		{
			_cancelToken = cts ?? new CancellationTokenSource();
			var query = BuildQurey();
			var url = new Uri($"{Config.ProxyAddress}/ws?{query}");

			int retryCount = 0;
			do
			{
				var result = await TryRunAsync(url);
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
