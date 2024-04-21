using AwoDevProxy.Shared.Messages;

namespace AwoDevProxy.Web.Api.Proxy
{
	public abstract class GenericResult<TResult, TImpl> where TImpl : GenericResult<TResult, TImpl>, new()
	{
		public bool Success => Error == null;
		public ProxyError Error { get; init; }
		public TResult Response { get; init; }

		public static TImpl FromError(ProxyError error) => new TImpl { Error = error };
		public static TImpl FromError(int status, string message) => new TImpl { Error = new ProxyError(status, message) };
		public static TImpl FromResponse(TResult response) => new TImpl { Response = response };
	}
}
