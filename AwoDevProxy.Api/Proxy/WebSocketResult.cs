using AwoDevProxy.Shared.Messages;
using Microsoft.AspNetCore.Mvc;

namespace AwoDevProxy.Api.Proxy
{
	public class WebSocketResult : GenericResult<ProxyWebSocketOpenAck, WebSocketResult>
	{
	}
}
