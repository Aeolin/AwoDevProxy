namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyConfig
	{
		public string FixedKey { get; set; }
		public string[] Domains { get; set; }
		public TimeSpan MaxTimeout { get; set; }
		public TimeSpan DefaultTimeout { get; set; }
	}
}
