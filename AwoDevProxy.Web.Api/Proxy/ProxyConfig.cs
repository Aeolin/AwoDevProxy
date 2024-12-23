﻿namespace AwoDevProxy.Web.Api.Proxy
{
	public class ProxyConfig
	{
		public string AuthParamName { get; set; } = "devprxy-auth";
		public string FixedKey { get; set; }
		public string[] Domains { get; set; }
		public TimeSpan MaxTimeout { get; set; }
		public TimeSpan DefaultTimeout { get; set; }
		public string DefaultAuthScheme { get; set; } = "Devprxy";
	}
}
