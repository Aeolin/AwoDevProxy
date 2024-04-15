using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	public static class ProxyConstants
	{
		public static readonly FrozenSet<string> HEADER_BLACKLIST = FrozenSet.ToFrozenSet(["transfer-encoding", "connection", "cache-control", "host"]);

	}
}
