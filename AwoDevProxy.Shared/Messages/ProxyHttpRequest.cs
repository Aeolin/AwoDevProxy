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

		[Key(1)]
		public string PathAndQuery { get; set; }

		[Key(2)]
		public Dictionary<string, string[]> Headers { get; set; }

		[Key(3)]
		public string Method { get; set; }

		[Key(4)]
		public int? TraceNumber { get; set; }

		[Key(5)]
		ProxyDataFrame Body { get; set; }

		public override string ToString() => $"{nameof(ProxyHttpRequest)}[[{TraceNumber}]{RequestId}]";

	}
}
