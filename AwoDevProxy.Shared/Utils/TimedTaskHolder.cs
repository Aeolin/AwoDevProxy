using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;

namespace AwoDevProxy.Shared.Utils
{
	public class TimedTaskHolder<TKey, TResult>
	{
		private readonly ConcurrentDictionary<TKey, TimedTaskCompletionSource<TResult>> _tasks;
		private readonly ObjectPool<TimedTaskCompletionSource<TResult>> _tcsPool;

		public TimedTaskHolder(int poolSize = 10)
		{
			_tcsPool = new DefaultObjectPool<TimedTaskCompletionSource<TResult>>(new TimedTaskHolderPoolPolicy(), poolSize);
			_tasks = new ConcurrentDictionary<TKey, TimedTaskCompletionSource<TResult>>();
		}

		public bool TrySetResult(TKey key, TResult result)
		{
			if (_tasks.TryGetValue(key, out var source))
				return source.TrySetResult(result);

			return false;
		}

		public void SetResult(TKey key, TResult result)
		{
			if (_tasks.TryGetValue(key, out var source))
				source.SetResult(result);
		}

		public async Task<TimeOutResult<TResult>> GetTask(TKey key, TimeSpan? timeout)
		{
			var source = _tcsPool.Get();
			_tasks.TryAdd(key, source);

			try
			{
				var result = await source.GetTask(timeout);
				return result;
			}
			finally
			{
				if(_tasks.Remove(key, out _))
					_tcsPool.Return(source);
			}
		}

		public void Dispose()
		{
			foreach(var task in _tasks.Values)	
				task.TrySetTimedOut();

			_tasks.Clear();
		}

		private class TimedTaskHolderPoolPolicy : IPooledObjectPolicy<TimedTaskCompletionSource<TResult>>
		{
			public TimedTaskCompletionSource<TResult> Create()
			{
				return new TimedTaskCompletionSource<TResult>();
			}

			public bool Return(TimedTaskCompletionSource<TResult> obj)
			{
				obj.Reset();
				return true;
			}
		}
	}
}
