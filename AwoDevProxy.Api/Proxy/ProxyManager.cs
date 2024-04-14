using AwoDevProxy.Shared;
using MessagePack;
using Microsoft.AspNetCore.Http.Extensions;
using System.Net.WebSockets;
using System.Text;

namespace AwoDevProxy.Api.Proxy
{
	public class ProxyManager
	{
		private readonly Dictionary<string, ProxyConnection> _connections = new Dictionary<string, ProxyConnection>();
		private readonly ProxyConfig _config;

		public bool ProxyExists(string name)
		{
			name = name.ToLower();
			if (_connections.TryGetValue(name, out ProxyConnection proxyConnection))
				return proxyConnection.Socket.State == WebSocketState.Open;

			return false;
		}

		private void RemoveProxy(ProxyConnection connection)
		{
			connection.SocketClosed -= Connection_SocketClosed;
			_connections.Remove(connection.Name);
		}

		private void AddProxy(ProxyConnection connection)
		{
			connection.SocketClosed += Connection_SocketClosed;
			_connections.Add(connection.Name, connection);
		}

		private void Connection_SocketClosed(ProxyConnection connection)
		{
			RemoveProxy(connection);
		}

		private void Connection_PacketReceived(ProxyConnection connection, byte[] data)
		{
			MessagePackSerializer.Deserialize<ProxyResponseModel>(data);
		}

		public async Task<ProxyConnection> SetupProxyAsync(string proxyName, WebSocket socket)
		{
			proxyName = proxyName.ToLower();
			if (_connections.TryGetValue(proxyName, out ProxyConnection proxyConnection))
			{
				proxyConnection.Close();
				_connections.Remove(proxyName);
			}

			proxyConnection = new ProxyConnection(proxyName, socket);
			AddProxy(proxyConnection);
			return proxyConnection;
		}

		private bool HealthCheck(ProxyConnection connection)
		{
			if (connection.Socket.State == WebSocketState.Closed)
			{
				connection.Close();
				return false;
			}

			return connection.Socket.State == WebSocketState.Open;
		}

		private async Task<bool> TryHandleRequestAsync(HttpContext context)
		{
			var request = context.Request;
			var response = context.Response;

			var proxyName = request.Path.Value.Split("/", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
			if (string.IsNullOrEmpty(proxyName))
				return false;

			if (_connections.TryGetValue(proxyName.ToLower(), out ProxyConnection proxyConnection) && HealthCheck(proxyConnection))
			{
				var pathAndQuery = request.GetEncodedPathAndQuery().Substring(proxyName.Length+1);
				var method = request.Method;
				byte[] body = null;
				if (request.Body != null)
				{
					using var memStream = new MemoryStream();
					await request.Body.CopyToAsync(memStream);
					body = memStream.ToArray();
				}

				var headers = request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray());
				var proxyRequest = new ProxyRequestModel { Headers = headers, PathAndQuery = pathAndQuery, Method = method, Body = body };
				var proxyResponse = await proxyConnection.SendRequestAsync(proxyRequest, null);

				if (proxyResponse == null)
				{
					response.StatusCode = 500;
					await response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes("Request timed out"));
					return true;
				}

				if (proxyResponse.StatusCode >= 300 && proxyResponse.StatusCode < 400)
				{
					var redirect = Encoding.UTF8.GetString(proxyResponse.Body);
					if (Uri.TryCreate(redirect, UriKind.Absolute, out var uri))
					{
						var newLink = $"{_config.BaseUrl}/{proxyConnection.Name}/{uri.PathAndQuery}";
						proxyResponse.Body = Encoding.UTF8.GetBytes(newLink);
					}
				}

				response.StatusCode = proxyResponse.StatusCode;
				if (proxyResponse.Headers != null)
					foreach (var header in proxyResponse.Headers)
						response.Headers[header.Key] = header.Value;

				if (proxyResponse.Body != null && proxyResponse.Body.Length > 0)
				{
					await response.BodyWriter.WriteAsync(proxyResponse.Body);
					await response.BodyWriter.FlushAsync();
				}

				return true;
			}
			else
			{
				return false;
			}
		}


		public async Task HandleAsync(HttpContext context, RequestDelegate next)
		{
			if (await TryHandleRequestAsync(context) == false)
				await next(context);
		}
	}
}
