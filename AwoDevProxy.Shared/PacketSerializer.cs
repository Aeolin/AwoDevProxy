using MessagePack;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	public class PacketSerializer
	{
		public static void Serialize<T>(object obj, Stream stream)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			if(PacketTypeMap<T>.TryGetKey(obj.GetType(), out var key))
			{
				MessagePackSerializer.Serialize(stream, key);
				MessagePackSerializer.Serialize(stream, obj);
			}
			else
			{
				throw new ArgumentException($"object doesn't have a known packet type");
			}
		}

		public static T Serialize<T>(object obj, IBufferWriter<byte> writer)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			if (PacketTypeMap<T>.TryGetKey(obj.GetType(), out var key))
			{
				MessagePackSerializer.Serialize(writer, key);
				MessagePackSerializer.Serialize(writer, obj);
				return key;
			}
			else
			{
				throw new ArgumentException($"object doesn't have a known packet type");
			}
		}

		public static object Deserialize<T>(Stream stream, out T key)
		{
			key = MessagePackSerializer.Deserialize<T>(stream);
			if(PacketTypeMap<T>.TryGetType(key, out var type))
			{
				return MessagePackSerializer.Deserialize(type, stream);
			}
			else
			{
				throw new ArgumentException($"Unknown packet type: {key}");
			}
		}
	}
}
