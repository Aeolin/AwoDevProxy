// See https://aka.ms/new-console-template for more information
using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;

var mem = new MemoryStream();
var obj = new ProxyWebSocketOpen { PathAndQuery = "/test/ws", Secure = true };
PacketSerializer.Serialize<MessageType>(obj, mem);
mem.Position = 0;
var packet = PacketSerializer.Deserialize<MessageType>(mem, out var key);
Console.WriteLine(key);