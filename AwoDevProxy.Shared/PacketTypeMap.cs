using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	public static class PacketTypeMap<T>
	{
		private static readonly FrozenDictionary<T, Type> _typeMapping;
		private static readonly FrozenDictionary<Type, T> _keyMapping;

		static PacketTypeMap()
		{
			_typeMapping = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(x => x.GetTypes())
				.Select(x => (type: x, attr: x.GetCustomAttribute<PacketTypeAttribute<T>>()))
				.Where(x => x.attr != null)
				.ToFrozenDictionary(x => x.attr.Id, x => x.type);

			_keyMapping = _typeMapping.ToFrozenDictionary(x => x.Value, x => x.Key);
		}

		public static bool TryGetKey(Type type, out T key) => _keyMapping.TryGetValue(type, out key);

		public static bool TryGetType(T key, out Type type) => _typeMapping.TryGetValue(key, out type);
	}
}
