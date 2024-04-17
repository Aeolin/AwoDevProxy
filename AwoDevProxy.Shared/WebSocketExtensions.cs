using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AwoDevProxy.Shared
{
	public static class WebSocketExtensions
	{
		private const int BUFFER_SIZE = 4096;
		private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create(BUFFER_SIZE, 256);

		public static async Task SendAsync(this WebSocket webSocket, Stream stream, CancellationToken token)
		{
			var countLeft = stream.Length;
			var buffer = _bufferPool.Rent(BUFFER_SIZE);
			while (countLeft > 0 && token.IsCancellationRequested == false)
			{
				var countRead = await stream.ReadAsync(buffer);
				countLeft -= countRead;
				if (countRead < BUFFER_SIZE)
				{
					await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, countRead), WebSocketMessageType.Binary, countLeft == 0, token);
				}
				else
				{
					await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, countLeft == 0, token);
				}
			}
		}

		public static async Task SendAsync(this WebSocket webSocket, ReadOnlySequence<byte> seq, CancellationToken token)
		{
			if (seq.IsEmpty)
				return;

			if (seq.IsSingleSegment)
			{
				await webSocket.SendAsync(seq.First, WebSocketMessageType.Binary, true, token);
			}
			else
			{				
				long countWritten = 0;
				foreach(var memory in seq)
				{
					countWritten += memory.Length;
					await webSocket.SendAsync(memory, WebSocketMessageType.Binary, countWritten == seq.Length, token);
				}	
			}
		}
	}
}
