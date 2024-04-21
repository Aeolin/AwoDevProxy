using AwoDevProxy.Shared.Messages;
using Microsoft.AspNetCore.Mvc;

namespace AwoDevProxy.Web.Api.Proxy
{
	public class WebSocketResult : GenericResult<ProxyWebSocketOpenAck, WebSocketResult>
	{
	}
}
