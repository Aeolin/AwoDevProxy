namespace AwoDevProxy.Api.Utils
{
	public static class TimeOutResult
	{
		public static TimeOutResult<T> TimeOut<T>() => TimeOutResult<T>.TimeOut();
		public static TimeOutResult<T> Success<T>(T result) => TimeOutResult<T>.Success(result);
	}

	public class TimeOutResult<T>
	{
		private static readonly TimeOutResult<T> _timedOut = new TimeOutResult<T> { TimedOut = true };
		
		public T Result { get; init; }
		public bool TimedOut { get; init; }

		public T ResultOrThrow(string message = null)
		{
			if (TimedOut)
				throw new TimeoutException(message);

			return Result;
		}

		private TimeOutResult() { }

		public static TimeOutResult<T> TimeOut() => _timedOut;
		public static TimeOutResult<T> Success(T result) => new TimeOutResult<T> { Result = result, TimedOut = false };
	}
}
