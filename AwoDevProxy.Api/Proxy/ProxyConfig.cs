namespace AwoDevProxy.Api.Proxy
{
	public class ProxyConfig
	{
		public string BaseUrl { get; set; }
		public string FixedKey { get; set; }
		public int SubdomainLevel { get; set; }
		public TimeSpan MaxTimeout { get; set; }
		public TimeSpan DefaultTimeout { get; set; }
	}
}
