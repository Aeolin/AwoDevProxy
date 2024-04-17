using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public interface IExceptionHandler<TSource>
	{
		public Type ExceptionType { get; }
		public Task<bool> HandleAsync(TSource source, Exception exception);
	}
}
