namespace AwoDevProxy.Web.Api.Service.Cookies
{
	public class CookieConfig
	{
		public int AesBlockSize { get; set; } = 128;
		public string SigningKey { get; set; }
		public string SigningIV { get; set; }
		public string FingerPrint { get; set; }
	}
}
