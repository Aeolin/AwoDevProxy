using AwoDevProxy.Web.Api.Middleware;
using AwoDevProxy.Web.Api.Proxy;
using AwoDevProxy.Web.Api.Service.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
config.AddEnvironmentVariables();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddOpenApi();
builder.Services.AddSingleton(config.GetSection("ProxyConfig").Get<ProxyConfig>());
builder.Services.AddSingleton(config.GetSection("CookieConfig").Get<CookieConfig>());
builder.Services.AddSingleton<ICookieService, CookieService>();
builder.Services.AddSingleton<RecyclableMemoryStreamManager>();
builder.Services.AddSingleton<IProxyManager, ProxyManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(opts =>
{
	opts.AllowAnyMethod();
	opts.AllowAnyHeader();
	opts.AllowCredentials();
	opts.SetIsOriginAllowed(x => true);
});

app.UseWebSockets();
app.UseMiddleware<ProxyRootingMiddleware>();
app.UseMiddleware<ProxyAuthenticationMiddleware>();
app.UseMiddleware<ProxyHandlingMiddleware>();


if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference(opts => {
		opts.WithTitle("Devproxy API")
		.WithTheme(ScalarTheme.DeepSpace)
		.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
	});
}

app.MapControllers();
app.Run();