using AwoDevProxy.Lib;
using Cocona;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Client
{
	public class Parameters : ICommandParameterSet
	{
		[Option("proxy", ['s'], Description = "The address of the proxy server to connect to.")]
		public required string ProxyAddress { get; set; }

		[Option("name", ['n'], Description = "The subdomain the client should be reachable.")]
		public required string Name { get; set; }

		[Option("local", ['l'], Description = "The local address to bind to.")]
		public required string LocalAddress { get; set; }

		[Option("key", ['k'], Description = "The auth key to use for the connection.")]
		public required string AuthKey { get; set; }

		[HasDefaultValue]
		[Option("buffer-size", ['b'], Description = "The size of the buffer to use for the connection.")]
		public int BufferSize { get; set; } = 8192;

		[HasDefaultValue]
		[Option("force", ['f'], Description = "Force the connection to be opened.")]
		public bool? ForceOpen { get; set; } = null;

		[HasDefaultValue]
		[Option("try-reopen", ['r'], Description = "Try to reopen the connection if it is closed.")]
		public bool TryReopen { get; set; } = false;

		[HasDefaultValue]
		[Option("server-timeout", ['t'], Description = "The timeout for the request.")]
		public TimeSpan? RequestTimeout { get; set; } = null;

		[HasDefaultValue]
		[Option("password", ['p'], Description = "The password which is needed for external requests")]
		public string Password { get; set; } = null;

		[HasDefaultValue]
		[Option("auth-header-scheme", ['a'], Description = "The name of the auth scheme to use.")]
		public string AuthHeaderScheme { get; set; } = null;

		[HasDefaultValue]
		[Option("allow-local-untrusted-certs", ['u'], Description = "Allow untrusted SSL certificates for local connections.")]
		public bool AllowLocalUntrustedCerts { get; set; } = false;
		public ProxyEndpointConfig BuildConfig()
		{
			return new ProxyEndpointConfig()
			{
				Name = this.Name,
				LocalAddress = this.LocalAddress,
				ProxyAddress = this.ProxyAddress,
				AuthKey = this.AuthKey,
				BufferSize = this.BufferSize,
				ForceOpen = this.ForceOpen,
				TryReopen = this.TryReopen,
				RequestTimeout = this.RequestTimeout,
				Password = this.Password,
				AuthHeaderScheme = this.AuthHeaderScheme,
				AllowLocalUntrustedCerts = this.AllowLocalUntrustedCerts
			};
		}
	}
}
