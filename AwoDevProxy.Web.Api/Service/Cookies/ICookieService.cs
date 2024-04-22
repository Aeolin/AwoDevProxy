namespace AwoDevProxy.Web.Api.Service.Cookies
{
	public interface ICookieService
	{
		public bool IsValid(string cookie, byte[] proxyFingerprint);
		public string CreateCookie(byte[] proxyFingerprint);
	}
}
