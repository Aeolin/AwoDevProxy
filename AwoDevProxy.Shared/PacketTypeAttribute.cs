using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
	internal class PacketTypeAttribute<T> : Attribute
	{
		public T Id { get; init; }

		public PacketTypeAttribute(T id)
		{
			Id=id;
		}
	}
}
