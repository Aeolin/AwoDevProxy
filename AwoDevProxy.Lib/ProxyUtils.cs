using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace AwoDevProxy.Lib
{
	internal static class ProxyUtils
	{
		internal static HttpRequestMessage CreateRequestFromProxy(ProxyHttpRequest request)
		{
			var httpRequest = new HttpRequestMessage
			{
				RequestUri = new Uri(request.PathAndQuery, UriKind.Relative),
				Method = HttpMethod.Parse(request.Method),
			};

			if (request.Body != null && request.Body.Length > 0)
				httpRequest.Content = new ByteArrayContent(request.Body);

			foreach (var header in request.Headers.Where(x => ProxyConstants.HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false))
				httpRequest.Headers.Add(header.Key, header.Value);

			return httpRequest;
		}

		internal static ProxyHttpResponse CreateResponseFromError(int statusCode, string message, Guid requestId)
		{
			return new ProxyHttpResponse { RequestId = requestId, StatusCode = statusCode, Body = Encoding.UTF8.GetBytes(message) };
		}

		internal static async Task<ProxyHttpResponse> CreateResponseFromHttpAsync(HttpResponseMessage response, Guid requestId)
		{
			var headers = response.Headers
				.Where(x => ProxyConstants.HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false)
				.ToDictionary(x => x.Key, x => x.Value.ToArray());

			byte[] body = null;
			if (response.Content != null)
			{
				body = await response.Content.ReadAsByteArrayAsync();
				foreach (var header in response.Content.Headers)
					headers[header.Key] = header.Value.ToArray();
			}

			return new ProxyHttpResponse
			{
				RequestId = requestId,
				StatusCode = (int)response.StatusCode,
				Headers = headers,
				Body = body,
			};
		}
	}
}
