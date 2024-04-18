using AwoDevProxy.Api.Middleware;
using System.Runtime.CompilerServices;

namespace AwoDevProxy.Api
{
	public static class Extensions
	{
		public static string GetUserHostAddress(this HttpContext context)
		{
			string ipList = context.GetServerVariable("HTTP_X_FORWARDED_FOR");

			if (!string.IsNullOrEmpty(ipList))
			{
				return ipList.Split(',')[0];
			}

			return context.GetServerVariable("REMOTE_ADDR");
		}

		public static ProxyRequestData GetProxyData(this HttpContext context)
		{
			if (context.Items.TryGetValue(nameof(ProxyRequestData), out var data))
				return data as ProxyRequestData;

			return default;
		}

		public static void AddProxyData(this HttpContext context, ProxyRequestData data)
		{
			context.Items[nameof(ProxyRequestData)] = data;
		}
	}
}
