﻿using AwoDevProxy.Web.Api.Middleware;
using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using Microsoft.AspNetCore.Http.Extensions;
using System.Buffers;
using System.Text;

namespace AwoDevProxy.Web.Api.Proxy
{
	public static class ProxyUtils
	{
		internal static async Task<ProxyHttpRequest> ConstructProxyRequestAsync(HttpRequest request, ProxyRequestData data = null)
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

			return new ProxyHttpRequest { RequestId = data?.RequestId ?? Guid.NewGuid(), TraceNumber = data?.TraceNumber, PathAndQuery = pathAndQuery, Headers = headers, Body = body, Method = method };
		}

		internal static ProxyWebSocketOpen ConstructWebSocketOpenRequest(HttpRequest request, Guid requestId)
		{
			var headers = ProxyConstants.FilterHeaders(request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray())).ToDictionary();
			var open = new ProxyWebSocketOpen { SocketId = requestId, PathAndQuery = request.GetEncodedPathAndQuery(), Headers =  headers };
			return open;
		}

		internal static async Task WriteErrorAsync(int status, string message, HttpResponse response)
		{
			response.StatusCode = status;
			await response.WriteAsync(message);
			await response.CompleteAsync();
		}

		internal static Task WriteErrorAsync(ProxyError error, HttpResponse response)
		{
			return WriteErrorAsync(error.StatusCode, error.Message, response);
		}

		internal static async Task WriteRedirectAsync(HttpResponse response, string url, int code = StatusCodes.Status302Found)
		{
			response.StatusCode = code;
			response.Headers.Location = url;
			await response.CompleteAsync();
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
