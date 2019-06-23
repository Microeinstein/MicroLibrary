#pragma warning disable CS0809
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace Micro.IO {
    public class SecureStream : Stream {
        public const int
            DEFAULT_AES_KEY_SIZE = 128,
            DEFAULT_AES_BLOCK_SIZE = 128;
        const byte ZERO = 0;

        public override bool CanRead
            => UsedStream.CanRead;
        public override bool CanSeek
            => false;
        public override bool CanWrite
            => UsedStream.CanWrite;
        [Obsolete("This class does not support Length.", true)]
        public override long Length
            => throw new NotSupportedException();
        [Obsolete("This class does not support Position.", true)]
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public Stream UsedStream { get; set; }
        public readonly byte[] WriterKey;
        public readonly byte[] WriterIV;
        readonly AesManaged aes;
        readonly int aesOutputSize;
        CryptoStream reader, writer;
        byte writedAmount = 0;
        int writedTotal = 0;

        public SecureStream(Stream underlying, int aesKeySize = DEFAULT_AES_KEY_SIZE, int aesBlockSize = DEFAULT_AES_BLOCK_SIZE) {
            UsedStream = underlying;
            aes = new AesManaged() {
                KeySize = aesKeySize,
                BlockSize = aesBlockSize,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.Zeros
            };
            WriterKey = aes.Key;
            WriterIV = aes.IV;
            var encryptor = aes.CreateEncryptor();
            aesOutputSize = encryptor.OutputBlockSize;
            writer = new CryptoStream(underlying, encryptor, CryptoStreamMode.Write);
        }
        public void InitializeReader(byte[] key, byte[] iv) {
            reader = new CryptoStream(UsedStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);
        }
        protected override void Dispose(bool disposing) {
            reader = null;
            writer = null;
            aes.Dispose();
        }

        public override void Flush() {
            if (writedAmount > 0) {
                byte negative = (byte)(aesOutputSize - writedAmount);
                var padding = ZERO.Repeat(negative);
                writer.Write(padding);
            }
            writedTotal = 0;
            writedAmount = 0;
            writer.Flush();
        }
        public override int Read(byte[] buffer, int offset, int count)
            => reader.Read(buffer, offset, count);
        public override void Write(byte[] buffer, int offset, int count) {
            writedTotal += count;
            writedAmount = (byte)(writedTotal % aesOutputSize);
            writer.Write(buffer, offset, count);
        }
        public override int ReadByte()
            => reader.ReadByte();
        public override void WriteByte(byte value) {
            writedTotal++;
            writedAmount = (byte)(writedTotal % aesOutputSize);
            writer.WriteByte(value);
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();
        public override void SetLength(long value)
            => throw new NotSupportedException();
    }
}
