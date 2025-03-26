using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Proxy;

namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyConnectionWebSocketRequest : ProxyConnectionRequestBase
	{
		private readonly WebSocketProxy _proxy;

		public override async Task<bool> WriteAsync(ProxyDataFrame frame)
		{
			await _proxy.SendAsync(frame);
			return true;
		}
	}
}
