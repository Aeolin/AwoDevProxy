namespace AwoDevProxy.Web.Api.Middleware
{
	public class ProxyRequestData
	{
		private static int _requestCounter = 0;
		public Guid RequestId { get; init; }
		public int TraceNumber { get; init; }
		public string LogValue => $"[{TraceNumber}]{RequestId}";

		public ProxyRequestData()
		{
			RequestId = Guid.NewGuid();
			TraceNumber = Interlocked.Increment(ref _requestCounter);
		}
	}
}
