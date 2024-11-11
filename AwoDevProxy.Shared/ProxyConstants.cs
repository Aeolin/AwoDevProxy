using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	public static class ProxyConstants
	{
		public static readonly FrozenSet<string> HEADER_BLACKLIST = FrozenSet.ToFrozenSet(["transfer-encoding", "cache-control", "host", "connection"]);
		public static readonly FrozenSet<string> WEBSOCKET_HEADER_WHITELIST = FrozenSet.ToFrozenSet(["cookie", "authentication"]);
		public static readonly FrozenSet<string> CONTENT_HEADERS = FrozenSet.ToFrozenSet(
			[
			  "Allow",
				"Content-Disposition",
				"Content-Type",
				"Content-Language",
				"Content-Encoding",
				"Content-Location",
				"Content-Range",
				"Expires",
				"Last-Modified"
			]
		);


		public static IEnumerable<KeyValuePair<string, IEnumerable<string>>> FilterHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) => headers.Where(x => HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false);
		public static IEnumerable<KeyValuePair<string, string[]>> FilterHeaders(IEnumerable<KeyValuePair<string, string[]>> headers) => headers.Where(x => HEADER_BLACKLIST.Contains(x.Key.ToLower()) == false);
		public static IEnumerable<KeyValuePair<string, string[]>> FilterWebsocketHeaders(IEnumerable<KeyValuePair<string, string[]>> headers) => headers.Where(x => WEBSOCKET_HEADER_WHITELIST.Contains(x.Key.ToLower()));
	}
}
