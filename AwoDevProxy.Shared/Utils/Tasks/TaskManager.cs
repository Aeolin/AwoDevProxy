using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public class TaskManager
	{
		private readonly Dictionary<Task, object> _currentObjects = new Dictionary<Task, object>();
		private readonly Dictionary<Type, ITaskHandler> _taskHandlers = new Dictionary<Type, ITaskHandler>();
		private readonly List<Task> _taskList = new List<Task>();
		private TaskCompletionSource _interrupt;
		private bool _stopRequested;

		public void Stop()
		{
			_stopRequested = true;
			_interrupt.SetResult();
			_interrupt = new TaskCompletionSource();
			_currentObjects.Clear();
		}

		public TaskManager WithTaskSource<TSource, TResult>(Func<TSource, Task<TResult>> taskGetter, Action<ITaskBuilder<TSource, TResult>> configure = null)
		{
			var handler = new TaskHandler<TSource, TResult>(taskGetter);
			configure?.Invoke(handler);
			_taskHandlers.Add(typeof(TSource), handler);
			_interrupt = new TaskCompletionSource();
			return this;
		}

		public void SubmitSource<TSource>(TSource obj)
		{
			var type = typeof(TSource);
			if (_taskHandlers.TryGetValue(type, out var handler) == false)
			{
				var handledType = _taskHandlers.Select(x => x.Key).FirstOrDefault(x => type.IsAssignableTo(x));
				if (handledType == null)
					throw new ArgumentException($"Can't handle objects of type {type.Name}", nameof(obj));

				handler = _taskHandlers[handledType];
				_taskHandlers.Add(type, handler);
			}

			_currentObjects.Add(handler.GetTask(obj), obj);
			_interrupt.TrySetResult();
		}

		public async Task<bool> AwaitNextTask()
		{
			if (_currentObjects.Count == 0)
				return false;

			if (_interrupt.Task.IsCompleted)
				_interrupt = new TaskCompletionSource();

			_stopRequested = false;
			_taskList.Clear();
			_taskList.AddRange(_currentObjects.Keys);
			_taskList.Add(_interrupt.Task);
			if (_taskList.Count < 2)
				return false;

			var task = await Task.WhenAny(_taskList);
			if (task == _interrupt.Task)
			{
				return _stopRequested == false;
			}
			else if (_currentObjects.TryGetValue(task, out var source))
			{
				_currentObjects.Remove(task);
				var handler = _taskHandlers[source.GetType()];
				if (await handler.HandleTaskResult(task, source))
				{
					_currentObjects.Add(handler.GetTask(source), source);
					Console.WriteLine("Readded task");
				}
			}

			return _currentObjects.Count > 0;
		}
	}
}
