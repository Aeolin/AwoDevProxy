using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AwoDevProxy.Lib
{
	public class ProxyEndpointConfig
	{
		public string LocalAddress { get; init; }
		public string ProxyServer { get; init; }
		public string Name { get; init; }
		public int BufferSize { get; init; }
		public string AuthKey { get; init; }
		public bool TryReopen { get; init; }

		[JsonConstructor]
		public ProxyEndpointConfig(string localAddress, string proxyServer, string name, string authKey, bool tryReopen, int bufferSize = 2048)
		{
			LocalAddress=localAddress;
			ProxyServer=proxyServer;
			Name=name;
			BufferSize=bufferSize;
			AuthKey=authKey;
			TryReopen = tryReopen;
		}
	}
}
