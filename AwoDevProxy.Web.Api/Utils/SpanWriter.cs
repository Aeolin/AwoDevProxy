using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AwoDevProxy.Web.Api.Utils
{
	public ref struct SpanWriter<T>
	{
		private Span<T> _span;
		private int _position;
		private int _length;

		public int Left => _length - _position;

		public SpanWriter(Span<T> span, int position = 0, int? length = null)
		{
			_span = span;
			_position = position;
			if (length.HasValue && length > span.Length)
				throw new ArgumentException("Length cannot be greater than span length.");

			_length = length ?? span.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write(Span<T> data)
		{
			if (data.Length > _span.Length - _position)
				throw new ArgumentOutOfRangeException(nameof(data));

			data.CopyTo(_span.Slice(_position));
			_position += data.Length;
		}

		public Span<T> GetSlice(int count)
		{
			if (count > _span.Length - _position)
				throw new ArgumentOutOfRangeException(nameof(count));
			
			var span = _span.Slice(_position, count);
			_position += count;
			return span;
		}
	}
}
