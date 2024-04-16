using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Messages
{
	[MessagePackFormatter(typeof(GenericEnumFormatter<MessageType>))]
	public enum MessageType : UInt16
	{
		HttpRequest = 1,
		HttpResponse = 2,
		WebSocketOpen = 3,
		WebSocketData = 4,
		WebSocketClose = 5,
		WebSocketOpenAck = 6
	}
}
