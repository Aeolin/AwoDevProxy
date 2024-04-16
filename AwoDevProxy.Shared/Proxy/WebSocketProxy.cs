using AwoDevProxy.Shared.Messages;
using System.Net.NetworkInformation;
using System.Net.WebSockets;

namespace AwoDevProxy.Shared.Proxy
{
	public class WebSocketProxy
	{
		public Guid Id { get; init; }
		public bool IsOpen => _cts.IsCancellationRequested == false && _socket.State.HasFlag(WebSocketState.Open);

		private CancellationTokenSource _cts;
		private readonly WebSocket _socket;
		private readonly byte[] _buffer;

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

		public async Task<ProxyWebSocketData> ReadAsync()
		{
			var result = await _socket.ReceiveAsync(_buffer, _cts.Token);
			var body = result.Count == _buffer.Length ? _buffer : _buffer.AsSpan().Slice(result.Count).ToArray();
			var data = new ProxyWebSocketData { Data = body, EndOfMessage = result.EndOfMessage, MessageType = result.MessageType, SocketId = Id };
			return data;
		}

		public async Task CloseAsync()
		{
			_cts.Cancel();
			await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
		}
	}
}
