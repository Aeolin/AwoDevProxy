using AwoDevProxy.Web.Api.Middleware;
using AwoDevProxy.Web.Api.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.IO;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
config.AddEnvironmentVariables();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(config.GetSection("ProxyConfig").Get<ProxyConfig>());
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


if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.MapControllers();
app.Run();