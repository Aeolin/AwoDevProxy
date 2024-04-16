using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	[PacketType<MessageType>(MessageType.WebSocketOpen)]
	public class ProxyWebSocketOpen
	{
		[Key(0)]
		public Guid SocketId { get; set; } = Guid.NewGuid();

		[Key(1)]
		public string PathAndQuery { get; set; }

		[Key(2)]
		public string Protocol { get; set; }
	}
}
