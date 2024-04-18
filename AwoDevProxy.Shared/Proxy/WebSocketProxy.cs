using AwoDevProxy.Shared.Messages;
using System.Net.NetworkInformation;
using System.Net.WebSockets;

namespace AwoDevProxy.Shared.Proxy
{
	public class WebSocketProxy
	{
		public Guid Id { get; init; }
		public bool IsOpen => _cts.IsCancellationRequested == false && _socket.State == WebSocketState.Open;

		private CancellationTokenSource _cts;
		private readonly WebSocket _socket;
		private readonly byte[] _buffer;

		public override string ToString() => $"{nameof(WebSocketProxy)}[{Id}]";

		public WebSocketProxy(Guid id, WebSocket socket, CancellationTokenSource cts = null)
		{
			Id=id;
			_cts=cts ?? new CancellationTokenSource();
			_socket=socket;
			_buffer=new byte[4096];
		}

		public async Task SendAsync(ProxyWebSocketData data)
		{
			await _socket.SendAsync(data.Data, data.MessageType, data.EndOfMessage, _cts.Token);
		}

		public async Task<WebSocketProxyReadResult> ReadAsync()
		{
			try
			{
				var result = await _socket.ReceiveAsync(_buffer, _cts.Token);
				var body = new ArraySegment<byte>(_buffer, 0, result.Count);
				var data = new ProxyWebSocketData { Data = body, EndOfMessage = result.EndOfMessage, MessageType = result.MessageType, SocketId = Id };
				return WebSocketProxyReadResult.Result(Id, data);
			}
			catch (Exception)
			{
				if (_cts.IsCancellationRequested == false)
					_cts.Cancel();

				await CloseAsync();
				return WebSocketProxyReadResult.Closed(Id);
			}
		}

		public async Task CloseAsync()
		{
			if (IsOpen)
			{
				_cts.Cancel(); 
				try
				{
					await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
				}
				catch (WebSocketException)
				{
					// do nothing, somehow websocket first realises it's in the aborted state once CloseAsync get's called
				}
			}
		}
	}
}
