using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	[MessagePackObject]
	public class ProxyResponseModel
	{
		[Key(0)]
		public Guid RequestId { get; set; }

		[Key(1)]
		public int StatusCode { get; set; }

		[Key(2)]
		public Dictionary<string, string[]> Headers { get; set; }

		[Key(3)]
		public byte[] Body { get; set; }
	}
}
