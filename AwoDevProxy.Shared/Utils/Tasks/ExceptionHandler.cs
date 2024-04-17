using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public class ExceptionHandler<TSource, TException> : IExceptionHandler<TSource> where TException : Exception
	{
		private Func<TSource, TException, Task<bool>> _handleExceptionAsync;
		private Func<TSource, TException, bool> _handleException;

		public ExceptionHandler(Func<TSource, TException, bool> handleException)
		{
			_handleException=handleException;
		}

		public ExceptionHandler(Func<TSource, TException, Task<bool>> handleExceptionAsync)
		{
			_handleExceptionAsync=handleExceptionAsync;
		}


		public Type ExceptionType => typeof(TException);

		public async Task<bool> HandleAsync(TSource source, Exception exception)
		{
			if (exception is TException tException)
			{
				if (_handleExceptionAsync != null)
				{
					return await _handleExceptionAsync(source, tException);
				}
				else if (_handleException != null)
				{
					return _handleException(source, tException);
				}
				else
				{
					return false;
				}
			}
			else
			{
				throw new ArgumentException($"expected exception to be of type {ExceptionType.Name}", nameof(exception));
			}
		}
	}
}
