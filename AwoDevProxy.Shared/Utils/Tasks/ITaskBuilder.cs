using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public interface ITaskBuilder<TSource, TResult>
	{
		ITaskBuilder<TSource, TResult> HandleException<TException>(Func<TSource, TException, bool> handle) where TException : Exception;
		ITaskBuilder<TSource, TResult> HandleException<TException>(Func<TSource, TException, Task<bool>> handle) where TException : Exception;
		ITaskBuilder<TSource, TResult> HandleResult(Func<TSource, TResult, Task<bool>> handle);
	}
}
