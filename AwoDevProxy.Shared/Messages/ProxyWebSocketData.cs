using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	public class ProxyWebSocketData
	{
		[Key(0)]
		public Guid SocketId { get; set; }

		[Key(1)]
		public bool EndOfMessage { get; set; }

		[Key(2)]
		public byte[] Data { get; set; }
	}
}
