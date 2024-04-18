using AwoDevProxy.Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.CommandLine;
using System.Reflection;


internal class Program
{
	public static async Task<int> Main(string[] args)
	{
		RootCommand rootCommand = new RootCommand("Opens a DevProxy");
		var localOption = new Option<string>("--local", "Local base url");
		var proxyOption = new Option<string>("--proxy", "Url of the proxy server");
		var nameOption = new Option<string>("--name", "The name the service should be available under the proxy");
		var authOption = new Option<string>("--key", "Auth key for proxy server");
		var bufferSizeOption = new Option<int>("--buffer-size", () => 2048, "Buffer size of the websockt");
		var tryReopen = new Option<bool>("--try-reopen", () => false, "Try to reopen proxy if connection failed or lost");
		var timeout = new Option<TimeSpan?>("--server-timeout", () => null, "Default request timeout for proxy server");
		rootCommand.AddOption(localOption);
		rootCommand.AddOption(proxyOption);
		rootCommand.AddOption(nameOption);
		rootCommand.AddOption(authOption);
		rootCommand.AddOption(bufferSizeOption);
		rootCommand.AddOption(tryReopen);
		rootCommand.AddOption(timeout);
		rootCommand.SetHandler(Run, localOption, proxyOption, nameOption, authOption, tryReopen, bufferSizeOption, timeout);

		return await rootCommand.InvokeAsync(args);
	}

	static async Task Run(string local, string proxy, string name, string authKey, bool tryReopen, int bufferSize = 2048, TimeSpan? timeout = null)
	{
		var config = new ProxyEndpointConfig(local, proxy, name, authKey, tryReopen, bufferSize, timeout);
		var factory = LoggerFactory.Create(opts => opts.AddConsole());
		var proxyClient = new ProxyEndpoint(config, factory);
		var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, _) => cts.Cancel();
		await proxyClient.RunAsync(cts);
	}
}