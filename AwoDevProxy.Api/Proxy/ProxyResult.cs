using AwoDevProxy.Shared;

namespace AwoDevProxy.Api.Proxy
{
	public class ProxyResult
	{
		public bool Success => Error == null;
		public ProxyError Error { get; init; }
		public ProxyResponseModel Response { get; init; }

		private ProxyResult() { }

		public static ProxyResult FromError(ProxyError error) => new ProxyResult { Error = error };
		public static ProxyResult FromError(int status, string message) => new ProxyResult { Error = new ProxyError(status, message) };
		public static ProxyResult FromResponse(ProxyResponseModel response) => new ProxyResult { Response = response };
	}
}
