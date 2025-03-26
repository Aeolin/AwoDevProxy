using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackObject]
	public class ProxyDataFrame
	{
		[Key(0)]
		public Guid RequestId { get; set; }

		[Key(1)]
		public int PacketCounter { get; set; }

		[Key(2)]
		public DataFrameType Type { get; set; }

		[Key(3)]
		public byte[] Data { get; set; }
	}
}
