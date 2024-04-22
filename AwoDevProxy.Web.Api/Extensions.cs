using AwoDevProxy.Web.Api.Middleware;
using System.Runtime.CompilerServices;

namespace AwoDevProxy.Web.Api
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

		public static byte[] SetLength(this byte[] array, int length, byte padding = 0)
		{
			if (array.Length == length)
				return array;

			if(array.Length < length)
			{
				var newArray = new byte[length];
				Array.Copy(array, newArray, array.Length);
				newArray.AsSpan().Slice(array.Length).Fill(padding);
				return newArray;
			}
			else
			{
				return array.AsSpan().Slice(length).ToArray();
			}
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
