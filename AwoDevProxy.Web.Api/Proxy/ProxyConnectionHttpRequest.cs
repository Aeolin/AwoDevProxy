
using AwoDevProxy.Shared.Messages;

namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyConnectionHttpRequest : ProxyConnectionRequestBase
	{
		private HttpResponse _response;

		public void BeginResponse(ProxyHttpResponse response)
		{
			ProxyUtils.WriteResponseHeaderToPipeline(response, _response);
		}

		public override async Task<bool> WriteAsync(ProxyDataFrame frame)
		{
			if(frame.PacketCounter != this.Counter + 1)
			{
				await ProxyUtils.WriteErrorAsync(500, "lost intermediate packet", _response);
				this.CompleteTask();
			}
			else
			{
				await _response.BodyWriter.WriteAsync(frame.Data);
				if(frame.Type == DataFrameType.Close)
				{
					await _response.BodyWriter.CompleteAsync();
					this.CompleteTask();
					return true;
				}
			}

			return false;
		}
	}
}
