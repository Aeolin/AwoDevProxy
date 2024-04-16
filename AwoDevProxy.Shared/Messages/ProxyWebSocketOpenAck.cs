using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	[PacketType<MessageType>(MessageType.WebSocketOpenAck)]
	public class ProxyWebSocketOpenAck
	{
		[Key(0)]
		public Guid SocketId { get; set; }

		[Key(1)]
		public bool Success { get; set; }

		[Key(2)]
		public int ResponseCode { get; set; }

		[Key(3)]
		public string ErrorMessage { get; set; }
	}
}
