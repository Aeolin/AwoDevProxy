namespace AwoDevProxy.Shared.Utils
{
	public class TimedTaskCompletionSource<T>
	{
		private TaskCompletionSource<T> _source = new TaskCompletionSource<T>();

		public void SetResult(T result) => _source.SetResult(result);
		public void SetCanceled() => _source.SetCanceled();
		public void SetException(Exception ex) => _source.SetException(ex);

		public bool TrySetResult(T result) => _source.TrySetResult(result);
		public bool TrySetCancelled() => _source.TrySetCanceled();
		public bool TrySetException(Exception ex) => _source.TrySetException(ex);

		public void Reset()
		{
			if (_source?.Task?.IsCompleted == false)
				_source.TrySetCanceled();

			_source = new TaskCompletionSource<T>();
		}

		public async Task<TimeOutResult<T>> GetTask(TimeSpan? timeout)
		{
			if (timeout.HasValue)
			{
				var task = _source.Task;
				var delay = Task.Delay(timeout.Value);
				var first = await Task.WhenAny(task, delay);
				if (first == delay)
				{
					return TimeOutResult.TimeOut<T>();
				}

				return TimeOutResult.Success(task.Result);
			}
			else
			{
				var res = await _source.Task;
				return TimeOutResult.Success(res);
			}
		}
	}
}
