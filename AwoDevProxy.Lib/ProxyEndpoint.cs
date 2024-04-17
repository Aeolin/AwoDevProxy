﻿using AwoDevProxy.Shared;
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

		private const int RECONNECT_DELAY = 5;
		private const int MAX_RECCONECT_DELAY = 60;

		public ProxyEndpoint(ProxyEndpointConfig config, ILoggerFactory factory = null, RecyclableMemoryStreamManager manager = null)
		{
			Config = config;
			_http = new HttpClient() { BaseAddress = new Uri(config.LocalAddress) };
			_buffer = new byte[config.BufferSize];
			_logger = factory?.CreateLogger<ProxyEndpoint>();
			_streamManager=manager ?? new RecyclableMemoryStreamManager();
			_webSocketProxies = new Dictionary<Guid, WebSocketProxy>();
			_taskManager = new TaskManager();
			SetupTasks(_taskManager);
		}

		private void SetupTasks(TaskManager manager)
		{
			manager.WithTaskSource<ClientWebSocket, WebSocketReceiveResult>(GetTask_Client, opts =>
			{
				opts.HandleResult(Handle_Client_ReadResultAsync);
				opts.HandleException<TaskCanceledException>(Handle_Client_TaskCancelledException);
			});

			manager.WithTaskSource<WebSocketProxy, WebSocketProxyReadResult>(GetTask_WebSocket, opts =>
			{
				opts.HandleResult(Handle_WebSocket_ReadResultAsync);
			});
		}


		private async Task HandlePacketAsync(Stream stream)
		{
			var packet = PacketSerializer.Deserialize<MessageType>(stream, out var type);
			object result = null;
			switch (type)
			{
				case MessageType.HttpRequest:
					result = await Handle_Client_HttpRequestAsync((ProxyHttpRequest)packet);
					break;

				case MessageType.WebSocketOpen:
					result = await Handle_WebSocket_OpenAsync((ProxyWebSocketOpen)packet);
					break;

				case MessageType.WebSocketData:
					await Handle_WebSocket_DataAsync((ProxyWebSocketData)packet);
					break;

				case MessageType.WebSocketClose:
					await Handle_WebSocket_CloseAsync((ProxyWebSocketClose)packet);
					break;
			}

			if (result != null)
				await SendPacketAsync(result);
		}

		private async Task SendPacketAsync(object packet)
		{
			var mem = _streamManager.GetStream();
			PacketSerializer.Serialize<MessageType>(packet, (IBufferWriter<byte>)mem);
			await _webSocket.SendAsync(mem.GetReadOnlySequence(), _cancelToken.Token);
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
