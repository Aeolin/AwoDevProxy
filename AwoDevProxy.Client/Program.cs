using AwoDevProxy.Client;
using AwoDevProxy.Lib;
using Cocona;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.Reflection;


var builder = CoconaApp.CreateBuilder(args);
builder.Services.AddLogging(x => x.AddConsole());
var app = builder.Build();
//app.AddCommand((Parameters parameters) => Run(parameters, app.Services.GetRequiredService<ILoggerFactory>()));

app.AddCommand(Run);
await app.RunAsync();

static async Task Run(Parameters parameters, ILoggerFactory factory)
{
	var config = parameters.BuildConfig();
	var proxyClient = new ProxyEndpoint(config, factory);
	var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, _) => cts.Cancel();
	await proxyClient.RunAsync(cts);
}
