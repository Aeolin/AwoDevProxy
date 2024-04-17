using AwoDevProxy.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Proxy
{
	public class WebSocketProxyReadResult
	{
		public bool IsOpen { get; init; }
		public WebSocketProxy Proxy { get; init; }
		public ProxyWebSocketData DataFrame { get; init; }

		public static WebSocketProxyReadResult Result(Guid socketId, ProxyWebSocketData data) => new WebSocketProxyReadResult { DataFrame = data, IsOpen = true };
		public static WebSocketProxyReadResult Closed(Guid socketId) => new WebSocketProxyReadResult { IsOpen = false };

		private WebSocketProxyReadResult()
		{

		}
	}
}
