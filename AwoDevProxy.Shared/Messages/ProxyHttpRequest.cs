using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	[PacketType<MessageType>(MessageType.HttpRequest)]
	public class ProxyHttpRequest
	{
		[Key(0)]
		public Guid RequestId { get; set; } = Guid.NewGuid();

		[Key(5)]
		public int? TraceNumber { get; set; }

		[Key(1)]
		public Dictionary<string, string[]> Headers { get; set; }

		[Key(2)]
		public string PathAndQuery { get; set; }

		[Key(3)]
		public string Method { get; set; }

		[Key(4)]
		public byte[] Body { get; set; }

	}
}
