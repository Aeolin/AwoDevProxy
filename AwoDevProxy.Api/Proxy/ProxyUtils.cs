using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using Microsoft.AspNetCore.Http.Extensions;
using System.Buffers;
using System.Text;

namespace AwoDevProxy.Api.Proxy
{
    public static class ProxyUtils
	{
		internal static async Task<ProxyHttpRequest> ConstructProxyRequestAsync(HttpRequest request)
		{
			var pathAndQuery = request.GetEncodedPathAndQuery();
			var headers = request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray());
			var method = request.Method;
			byte[] body = null;
			if (request.Body != null)
			{
				var result = await request.BodyReader.ReadAsync();
				if (result.IsCompleted)
					body = result.Buffer.ToArray();
			}

			return new ProxyHttpRequest { PathAndQuery = pathAndQuery, Headers = headers, Body = body, Method = method };
		}

		internal static async Task WriteErrorAsync(ProxyError error, HttpResponse response)
		{
			response.StatusCode = error.StatusCode;
			await response.WriteAsync(error.Message);
		}

		internal static async Task WriteResponseToPipelineAsync(ProxyHttpResponse proxyResponse, HttpResponse response)
		{
			response.StatusCode = proxyResponse.StatusCode;

			if (proxyResponse.Headers != null)
			{
				var headers = proxyResponse.Headers.Where(x => ProxyConstants.HEADER_BLACKLIST.Contains(x.Key) == false);
				foreach (var header in headers)
					response.Headers.Append(header.Key, header.Value);
			}

			if (proxyResponse.Body != null)
				await response.BodyWriter.WriteAsync(proxyResponse.Body);
			else
				await response.BodyWriter.CompleteAsync();

		}

		internal static async Task WriteResultAsync(int statusCode, HttpResponse response)
		{
			response.StatusCode = statusCode;
			await response.CompleteAsync();
		}
	}
}
