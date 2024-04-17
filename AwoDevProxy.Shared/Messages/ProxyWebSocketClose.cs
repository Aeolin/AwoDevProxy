using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	[PacketType<MessageType>(MessageType.WebSocketClose)]
	public class ProxyWebSocketClose
	{
		[Key(0)]
		public Guid SocketId { get; set; }
	}
}
