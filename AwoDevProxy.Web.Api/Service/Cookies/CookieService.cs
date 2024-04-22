using AwoDevProxy.Web.Api.Proxy;
using AwoDevProxy.Web.Api.Utils;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace AwoDevProxy.Web.Api.Service.Cookies
{
	public class CookieService : ICookieService
	{
		const int ProxyFingerprintSize = 16;
		const int TimeStampSize = sizeof(long);

		private MemoryPool<byte> _memoryPool;
		private CookieConfig _config;
		private byte[] _fingerPrint;
		private readonly Aes _aes;

		private readonly int _cookieSize;
		private readonly int _aesAlignedCookieSize;
		private readonly int _aesBlockBytes;

		public CookieService(CookieConfig config)
		{
			_config = config;
			_memoryPool = MemoryPool<byte>.Shared;
			_fingerPrint = Encoding.ASCII.GetBytes(config.FingerPrint);
			_cookieSize = _fingerPrint.Length + ProxyFingerprintSize + TimeStampSize;
			_aes = AesCng.Create();
			_aes.BlockSize = _config.AesBlockSize;
			_aesBlockBytes = _aes.BlockSize / 8;
			_aes.IV = Convert.FromBase64String(_config.SigningIV).SetLength(_aesBlockBytes);
			_aes.Key = MD5.HashData(Encoding.UTF8.GetBytes(_config.SigningKey));
			_aesAlignedCookieSize = AesAlignedSize(_cookieSize);
		}

		private int AesAlignedSize(int size) => (int)Math.Ceiling(size / (double)_aesBlockBytes) * _aesBlockBytes;

		public string CreateCookie(byte[] proxyFingerprint)
		{
			var payloadBuffer = _memoryPool.Rent(_aesAlignedCookieSize);
			var payloadSpan = payloadBuffer.Memory.Span;
			var writer = new SpanWriter<byte>(payloadSpan, 0, _cookieSize);
			var salt = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
			writer.Write(_fingerPrint);
			writer.Write(proxyFingerprint);
			writer.Write(salt);

			using var base64Buffer = _memoryPool.Rent(Math.Max(_aesAlignedCookieSize, Base64.GetMaxEncodedToUtf8Length(_cookieSize)));
			var base64Span = base64Buffer.Memory.Span;
			var encryptor = new SpanSymmetricAlgorithm(_aes, payloadSpan, base64Span, _cookieSize);
			var written = encryptor.Encrypt();
			payloadBuffer.Dispose();
			var result = Base64.EncodeToUtf8InPlace(base64Span, _cookieSize, out written);
			return Encoding.UTF8.GetString(base64Span.Slice(0, written));
		}

		public bool IsValid(string cookie, byte[] proxyFingerprint)
		{
			var utf8Len = Encoding.UTF8.GetByteCount(cookie);
			var cookieBuffer = _memoryPool.Rent(AesAlignedSize(utf8Len));
			var cookieSpan = cookieBuffer.Memory.Span;
			var written = Encoding.UTF8.GetBytes(cookie, cookieSpan);

			if (Base64.DecodeFromUtf8InPlace(cookieSpan.Slice(0, written), out written) == OperationStatus.Done)
			{
				if (written != _cookieSize)
					return false;

				var cryptoBuffer = _memoryPool.Rent(_aesAlignedCookieSize);
				var cryptoSpan = cryptoBuffer.Memory.Span;
				var decryptor = new SpanSymmetricAlgorithm(_aes, cookieSpan, cryptoSpan, _cookieSize);
				written = decryptor.Decrypt();
				cookieBuffer.Dispose();
				var reader = new SpanReader<byte>(cryptoSpan, 0, written);
				if (reader.Read(_fingerPrint.Length).SequenceEqual(_fingerPrint) == false)
					return false;

				if (reader.Read(ProxyFingerprintSize).SequenceEqual(proxyFingerprint) == false)
					return false;

				var timestamp = BitConverter.ToInt64(reader.Read(TimeStampSize));
				cryptoBuffer.Dispose();
				return timestamp < DateTime.UtcNow.Ticks;
			}
			else
			{
				cookieBuffer.Dispose();
			}

			return false;
		}
	}
}
