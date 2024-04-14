using AwoDevProxy.Api.Proxy;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(config.GetSection("ProxyConfig").Get<ProxyConfig>());
builder.Services.AddSingleton<ProxyManager>();

var app = builder.Build();
var manager = app.Services.GetRequiredService<ProxyManager>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseWebSockets();

//app.UseAuthorization();
app.Use(manager.HandleAsync);

app.MapControllers();

app.Run();
