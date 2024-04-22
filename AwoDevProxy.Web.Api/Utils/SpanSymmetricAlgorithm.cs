using System.Security.Cryptography;

namespace AwoDevProxy.Web.Api.Utils
{
	public ref struct SpanSymmetricAlgorithm
	{
		private SpanReader<byte> _reader;
		private SpanWriter<byte> _writer;
		private int _blockSize;
		private int _length;
		private SymmetricAlgorithm _crypto;

		public SpanSymmetricAlgorithm(SymmetricAlgorithm algorithm, Span<byte> from, Span<byte> to, int? length = null)
		{
			_length = length ?? from.Length;

			if (to.Length < length || from.Length < length)
				throw new ArgumentException($"From and to must be at least of length {length}");

			_reader = new SpanReader<byte>(from);
			_writer = new SpanWriter<byte>(to);
			_crypto = algorithm;
			_blockSize = algorithm.BlockSize / 8;
		}

		public int Encrypt()
		{
			int written = 0;
			while(_reader.Left >= _blockSize && written < _length)
			{
				var block = _reader.Read(_blockSize);
				_crypto.EncryptEcb(block, _writer.GetSlice(_blockSize), PaddingMode.None);
				written += _blockSize;
			}

			return Math.Min(_length, written);
		}

		public int Decrypt()
		{
			int written = 0;
			while (_reader.Left >= _blockSize && written < _length)
			{
				var block = _reader.Read(_blockSize);
				_crypto.DecryptEcb(block, _writer.GetSlice(_blockSize), PaddingMode.None);
				written += _blockSize;
			}

			return Math.Min(_length, written);
		}
	}
}
