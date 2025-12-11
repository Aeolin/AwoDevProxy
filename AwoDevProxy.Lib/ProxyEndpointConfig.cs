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
		public string ProxyAddress { get; init; }
		public string Name { get; init; }
		public int BufferSize { get; init; }
		public string AuthKey { get; init; }
		public bool TryReopen { get; init; }
		public TimeSpan? RequestTimeout { get; init; }
		public string Password { get; init; }
		public string AuthHeaderScheme { get; init; }
		public bool IsLocalSecure => LocalAddress.StartsWith("https") || LocalAddress.StartsWith("wss");
		public string WebSocketScheme => IsLocalSecure ? "wss" : "ws";
		public bool? ForceOpen { get; init; }
		public bool AllowLocalUntrustedCerts { get; set; }

		public ProxyEndpointConfig()
		{

		}

		[JsonConstructor]
		public ProxyEndpointConfig(string localAddress, string proxyServer, string name, string authKey, bool tryReopen, string? password = null, string? authHeaderScheme = null, bool? force = null, int bufferSize = 2048, TimeSpan? requestTimeout = null)
		{
			LocalAddress=localAddress;
			ProxyAddress=proxyServer;
			Name=name;
			BufferSize=bufferSize;
			AuthKey=authKey;
			TryReopen = tryReopen;
			RequestTimeout=requestTimeout;
			ForceOpen = force;
			Password = password;
			AuthHeaderScheme = authHeaderScheme;
		}
	}
}
