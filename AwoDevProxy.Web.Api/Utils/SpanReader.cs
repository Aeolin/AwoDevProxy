using System.Runtime.CompilerServices;

namespace AwoDevProxy.Web.Api.Utils
{
	public ref struct SpanReader<T>
	{
		private Span<T> _span;
		private int _position;
		private int _length;

		public int Left => _length - _position;

		public SpanReader(Span<T> span, int position = 0, int? length = null)
		{
			_span = span;
			_position = position;
			if (length.HasValue && length > span.Length)
				throw new ArgumentException("Length cannot be greater than span length.");

			_length = length ?? span.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<T> Read(int count)
		{
			if (count < 0 || count > Left)
				throw new ArgumentOutOfRangeException(nameof(count));

			var slice = _span.Slice(_position, count);
			_position += count;
			return slice;
		}
	}
}
