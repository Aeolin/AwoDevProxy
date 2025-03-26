using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Shared.Utils;

namespace AwoDevProxy.Web.Api.Proxy
{
	public abstract class ProxyConnectionRequestBase
	{
		private readonly TaskCompletionSource _tcs = new();
		protected void CompleteTask() => _tcs.SetResult();

		public Task Task => _tcs.Task;


		public Guid PacketId { get; }
		public int Counter { get; }

		public abstract Task<bool> WriteAsync(ProxyDataFrame frame);
	}
}
