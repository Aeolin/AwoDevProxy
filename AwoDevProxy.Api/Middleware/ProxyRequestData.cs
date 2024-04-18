namespace AwoDevProxy.Api.Middleware
{
	public class ProxyRequestData
	{
		public Guid RequestId { get; init; }
	
		public ProxyRequestData()
		{
			RequestId = Guid.NewGuid();
		}
	}
}
