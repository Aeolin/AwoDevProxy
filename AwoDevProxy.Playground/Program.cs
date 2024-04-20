// See https://aka.ms/new-console-template for more information
using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;

HttpClient _client = new HttpClient() { BaseAddress = new Uri("http://127.0.0.1:8888") };


var result = await _client.GetAsync("favicon.ico");
var content = await result.Content.ReadAsByteArrayAsync();