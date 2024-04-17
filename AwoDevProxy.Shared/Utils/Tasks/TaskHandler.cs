using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public class TaskHandler<TSource, TResult> : ITaskHandler, ITaskBuilder<TSource, TResult>
	{
		public Type SourceType => typeof(TSource);
		private Func<TSource, Task<TResult>> _taskGetter { get; init; }
		private Func<TSource, TResult, Task<bool>> _resultHandler { get; set; }
		private readonly List<IExceptionHandler<TSource>> _exceptionHandlers = new List<IExceptionHandler<TSource>>();

		public TaskHandler(Func<TSource, Task<TResult>> taskGetter)
		{
			_taskGetter = taskGetter;
		}

		public ITaskBuilder<TSource, TResult> HandleResult(Func<TSource, TResult, Task<bool>> handle)
		{
			_resultHandler = handle;
			return this;
		}

		public ITaskBuilder<TSource, TResult> HandleException<TException>(Func<TSource, TException, bool> handle) where TException : Exception
		{
			_exceptionHandlers.Add(new ExceptionHandler<TSource, TException>(handle));
			return this;
		}

		public ITaskBuilder<TSource, TResult> HandleException<TException>(Func<TSource, TException, Task<bool>> handle) where TException : Exception
		{
			_exceptionHandlers.Add(new ExceptionHandler<TSource, TException>(handle));
			return this;
		}

		public Task GetTask(object source)
		{
			if (source is TSource tSource)
			{
				return _taskGetter(tSource);
			}
			else
			{
				throw new ArgumentException($"expected source to by of type {SourceType.Name}");
			}
		}

		public async Task<bool> HandleTaskResult(Task task, object source)
		{
			if (_resultHandler == null)
				return true;

			if (task is Task<TResult> tTask && source is TSource tSource)
			{
				if (task.IsCompleted)
				{
					try
					{
						return await _resultHandler(tSource, tTask.Result);
					}
					catch (Exception ex)
					{
						foreach (var handler in _exceptionHandlers)
							if (ex.GetType().IsAssignableTo(handler.ExceptionType))
								return await handler.HandleAsync(tSource, ex);
						
						throw;
					}
				}
				else
				{
					throw new InvalidOperationException($"{nameof(HandleResult)} should only be called when task ran to completion");
				}
			}
			else
			{
				throw new ArgumentException($"expected task to be of {typeof(Task<TResult>).Name} and source to be of {SourceType.Name}");
			}
		}
	}
}
