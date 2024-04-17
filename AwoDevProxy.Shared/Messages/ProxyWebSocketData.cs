using AwoDevProxy.Shared.Utils;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	[PacketType<MessageType>(Messages.MessageType.WebSocketData)]
	public class ProxyWebSocketData
	{
		[Key(0)]
		public Guid SocketId { get; set; }

		[Key(1)]
		public WebSocketMessageType MessageType { get; set; }

		[Key(2)]
		public bool EndOfMessage { get; set; }

		[Key(3)]	
		public ArraySegment<byte> Data { get; set; }
	}
}
