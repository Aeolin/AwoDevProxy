using AwoDevProxy.Lib;
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
		rootCommand.AddOption(localOption);
		rootCommand.AddOption(proxyOption);
		rootCommand.AddOption(nameOption);
		rootCommand.AddOption(authOption);
		rootCommand.AddOption(bufferSizeOption);
		rootCommand.SetHandler(Run, localOption, proxyOption, nameOption, authOption, bufferSizeOption);

		return await rootCommand.InvokeAsync(args);
	}

	static async Task Run(string local, string proxy, string name, string authKey, int bufferSize = 2048)
	{
		var config = new ProxyEndpointConfig(local, proxy, name, authKey, bufferSize);
		var proxyClient = new ProxyEndpoint(config);
		await proxyClient.RunAsync();
	}
}