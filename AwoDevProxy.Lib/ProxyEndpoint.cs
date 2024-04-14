using AwoDevProxy.Shared;
using MessagePack;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Lib
{
	public class ProxyEndpoint
	{
		private readonly HttpClient _http;
		private ClientWebSocket _webSocket;
		public ProxyEndpointConfig Config { get; init; }
		private readonly CancellationTokenSource _cancelToken;
		private readonly byte[] _buffer;
		private readonly MemoryStream _currentPacket;
		private static readonly FrozenSet<string> HEADER_BLACKLIST = FrozenSet.ToFrozenSet(["transfer-encoding", "connection", "cache-control"]);


		public ProxyEndpoint(ProxyEndpointConfig config)
		{
			Config = config;
			_http = new HttpClient() { BaseAddress = new Uri(config.LocalAddress) };
			_webSocket = new ClientWebSocket();
			_cancelToken = new CancellationTokenSource();
			_buffer = new byte[config.BufferSize];
			_currentPacket = new MemoryStream();
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

				foreach (var header in request.Headers)
					httpRequest.Headers.Add(header.Key, header.Value);


				var response = await _http.SendAsync(httpRequest);
				var headers = response.Headers
					.Where(x => HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false)
					.ToDictionary(x => x.Key, x => x.Value.ToArray());


				byte[] body = null;
				if (response.Content != null)
				{
					body = await response.Content.ReadAsByteArrayAsync();
					foreach (var header in response.Content.Headers)
						headers[header.Key] = header.Value.ToArray();
				}

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
				return new ProxyResponseModel
				{
					RequestId = request.RequestId,
					StatusCode = 500,
					Headers = null,
					Body = Encoding.UTF8.GetBytes(ex.ToString())
				};

			}
		}

		public async Task RunAsync()
		{
			var uri = new Uri($"{Config.ProxyServer}/ws/{Config.Name}?authKey={Config.AuthKey}");
			await _webSocket.ConnectAsync(uri, _cancelToken.Token);

			while (_webSocket.State.HasFlag(WebSocketState.Open) && _cancelToken.IsCancellationRequested == false)
			{
				try
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
				catch (Exception)
				{
					throw;
				}
			}

		}
	}
}
