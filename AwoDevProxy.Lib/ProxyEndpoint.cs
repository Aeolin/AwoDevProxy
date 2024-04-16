using AwoDevProxy.Shared;
using MessagePack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
		private ClientWebSocket _webSocket;
		private readonly byte[] _buffer;
		private readonly MemoryStream _currentPacket;
		private const int RECONNECT_DELAY = 5;
		private const int MAX_RECCONECT_DELAY = 60;
		private ILogger _logger;

		public ProxyEndpoint(ProxyEndpointConfig config, ILoggerFactory factory = null)
		{
			Config = config;
			_http = new HttpClient() { BaseAddress = new Uri(config.LocalAddress) };
			_buffer = new byte[config.BufferSize];
			_currentPacket = new MemoryStream();
			_logger = factory?.CreateLogger<ProxyEndpoint>();
		}

		private async Task<ProxyResponseModel> HandleRequestAsync()
		{
			var data = _currentPacket.ToArray();
			_currentPacket.SetLength(0);
			var request = MessagePackSerializer.Deserialize<ProxyRequestModel>(data);

			try
			{

				var httpRequest = new HttpRequestMessage
				{
					RequestUri = new Uri(request.PathAndQuery, UriKind.Relative),
					Method = HttpMethod.Parse(request.Method),
				};

				if (request.Body != null && request.Body.Length > 0)
					httpRequest.Content = new ByteArrayContent(request.Body);

				foreach (var header in request.Headers.Where(x => ProxyConstants.HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false))
					httpRequest.Headers.Add(header.Key, header.Value);


				var response = await _http.SendAsync(httpRequest);
				var headers = response.Headers
					.Where(x => ProxyConstants.HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false)
					.ToDictionary(x => x.Key, x => x.Value.ToArray());


				byte[] body = null;
				if (response.Content != null)
				{
					body = await response.Content.ReadAsByteArrayAsync();
					foreach (var header in response.Content.Headers)
						headers[header.Key] = header.Value.ToArray();
				}

				_logger?.LogInformation($"Handeled request for {request.PathAndQuery}, response: {response.StatusCode}");
				return new ProxyResponseModel
				{
					RequestId = request.RequestId,
					StatusCode = (int)response.StatusCode,
					Headers = headers,
					Body = body,
				};


			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, $"Error forwarding request {request.PathAndQuery}");
				return new ProxyResponseModel
				{
					RequestId = request.RequestId,
					StatusCode = 500,
					Headers = null,
					Body = Encoding.UTF8.GetBytes(ex.ToString())
				};

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
				while (_webSocket.State.HasFlag(WebSocketState.Open) && _cancelToken.IsCancellationRequested == false)
				{
					var received = await _webSocket.ReceiveAsync(_buffer, _cancelToken.Token);
					_currentPacket.Write(_buffer, 0, received.Count);
					if (received.EndOfMessage)
					{
						var response = await HandleRequestAsync();
						var responseData = MessagePackSerializer.Serialize(response);
						await _webSocket.SendAsync(responseData, WebSocketMessageType.Binary, true, _cancelToken.Token);
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
