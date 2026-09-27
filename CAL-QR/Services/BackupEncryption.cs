using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CAL_QR.Services
{
    /// <summary>
    /// كلمة سرّ خاطئة أو ملفّ نسخة احتياطيّة معدَّل أو تالف. الحالتان لا تُميَّزان عمداً:
    /// رمز المصادقة لا يطابق في الحالتين، والتمييز بينهما يكشف للمهاجم أيّهما أصاب.
    /// </summary>
    public class BackupPasswordException : Exception
    {
        public BackupPasswordException(string message) : base(message) { }
    }

    /// <summary>
    /// تشفير ملفّ النسخة الاحتياطيّة بكلمة سرّ.
    ///
    /// لماذا: قاعدة البيانات تحمل مفتاح HMAC الذي يوقّع الشهادات. نسخة احتياطيّة مكشوفة
    /// تعني أنّ من يحصل عليها يستطيع توليد شهادات مزوَّرة تجتاز التحقّق.
    ///
    /// الصيغة (CALQRBK1):
    ///   [8] "CALQRBK1" · [16] ملح · [4] عدد تكرارات PBKDF2 (little-endian) · [16] IV
    ///   · النصّ المشفَّر (AES-256-CBC، PKCS7) · [32] HMAC-SHA256
    ///
    /// المفتاحان (تشفير ومصادقة) يُشتقّان معاً من كلمة السرّ بـPBKDF2-SHA256 (٦٤ بايت).
    /// الـHMAC يغطّي الرأس والنصّ المشفَّر (تشفير ثمّ مصادقة)، ويُتحقَّق منه قبل أيّ فكّ،
    /// فلا يُكتب بايت واحد من محتوى معدَّل على القرص.
    ///
    /// CBC+HMAC لا AES-GCM: الملفّ قد يكبر بالمرفقات، وAesGcm في .NET يعمل على الذاكرة
    /// كاملة دفعة واحدة؛ CryptoStream يعالج الملفّ تدفّقاً مهما كبر.
    /// </summary>
    public static class BackupEncryption
    {
        public const string FileExtension = ".cqbak";
        public const int DefaultIterations = 600_000;

        // حدّا القبول عند القراءة: رأس معدَّل بتكرارات هائلة يعلّق الاشتقاق دقائق،
        // وتكرارات ضئيلة تعني ملفّاً لم يُنشئه هذا الصنف.
        private const int MinIterations = 10_000;
        private const int MaxIterations = 10_000_000;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CALQRBK1");
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int TagSize = 32;
        private const int HeaderSize = 8 + SaltSize + 4 + IvSize;

        private const string WrongPasswordMessage =
            "كلمة سرّ النسخة الاحتياطيّة غير صحيحة، أو الملفّ تالف أو معدَّل.";

        /// <summary>هل يبدأ الملفّ بتوقيع صيغة النسخ المشفَّرة؟ لا يتحقّق من كلمة السرّ.</summary>
        public static bool IsEncryptedBackup(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            var buffer = new byte[Magic.Length];
            int read = ReadFully(stream, buffer, 0, buffer.Length);
            return read == Magic.Length && CryptographicOperations.FixedTimeEquals(buffer, Magic);
        }

        public static void EncryptFile(string sourcePath, string destinationPath, string password,
            int iterations = DefaultIterations)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("كلمة سرّ النسخة الاحتياطيّة فارغة.", nameof(password));
            }

            if (iterations < MinIterations || iterations > MaxIterations)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
            DeriveKeys(password, salt, iterations, out byte[] encKey, out byte[] macKey);

            try
            {
                byte[] header = new byte[HeaderSize];
                Buffer.BlockCopy(Magic, 0, header, 0, Magic.Length);
                Buffer.BlockCopy(salt, 0, header, Magic.Length, SaltSize);
                WriteInt32LittleEndian(header, Magic.Length + SaltSize, iterations);
                Buffer.BlockCopy(iv, 0, header, Magic.Length + SaltSize + 4, IvSize);

                using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, macKey);
                using var aes = Aes.Create();
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    output.Write(header, 0, header.Length);
                    hmac.AppendData(header);

                    using (var macTee = new MacTeeStream(output, hmac))
                    using (var encryptor = aes.CreateEncryptor())
                    using (var crypto = new CryptoStream(macTee, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                    using (var input = File.OpenRead(sourcePath))
                    {
                        input.CopyTo(crypto);
                        crypto.FlushFinalBlock();
                    }

                    byte[] tag = hmac.GetHashAndReset();
                    output.Write(tag, 0, tag.Length);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encKey);
                CryptographicOperations.ZeroMemory(macKey);
            }
        }

        /// <summary>
        /// يتحقّق من رمز المصادقة على الملفّ كاملاً أوّلاً، ثمّ يفكّ التشفير إلى الوجهة.
        /// كلمة سرّ خاطئة أو ملفّ معدَّل ⇒ BackupPasswordException ولا تُنشأ الوجهة.
        /// </summary>
        public static void DecryptFile(string sourcePath, string destinationPath, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new BackupPasswordException(WrongPasswordMessage);
            }

            using var input = File.OpenRead(sourcePath);
            long length = input.Length;
            if (length < HeaderSize + TagSize)
            {
                throw new BackupPasswordException(WrongPasswordMessage);
            }

            byte[] header = new byte[HeaderSize];
            if (ReadFully(input, header, 0, HeaderSize) != HeaderSize ||
                !CryptographicOperations.FixedTimeEquals(header.AsSpan(0, Magic.Length), Magic))
            {
                throw new InvalidDataException("الملفّ ليس نسخة احتياطيّة مشفَّرة من CAL-QR.");
            }

            byte[] salt = header.AsSpan(Magic.Length, SaltSize).ToArray();
            int iterations = ReadInt32LittleEndian(header, Magic.Length + SaltSize);
            byte[] iv = header.AsSpan(Magic.Length + SaltSize + 4, IvSize).ToArray();

            if (iterations < MinIterations || iterations > MaxIterations)
            {
                throw new BackupPasswordException(WrongPasswordMessage);
            }

            DeriveKeys(password, salt, iterations, out byte[] encKey, out byte[] macKey);

            try
            {
                long cipherLength = length - HeaderSize - TagSize;

                // ١. المصادقة قبل الفكّ
                using (var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, macKey))
                {
                    hmac.AppendData(header);
                    CopyRange(input, cipherLength, chunk => hmac.AppendData(chunk));

                    byte[] storedTag = new byte[TagSize];
                    if (ReadFully(input, storedTag, 0, TagSize) != TagSize)
                    {
                        throw new BackupPasswordException(WrongPasswordMessage);
                    }

                    byte[] computedTag = hmac.GetHashAndReset();
                    if (!CryptographicOperations.FixedTimeEquals(computedTag, storedTag))
                    {
                        throw new BackupPasswordException(WrongPasswordMessage);
                    }
                }

                // ٢. الفكّ — بعد ثبوت سلامة الملفّ
                input.Position = HeaderSize;

                using var aes = Aes.Create();
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                using var decryptor = aes.CreateDecryptor();
                using var crypto = new CryptoStream(output, decryptor, CryptoStreamMode.Write);
                CopyRange(input, cipherLength, chunk => crypto.Write(chunk));
                crypto.FlushFinalBlock();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encKey);
                CryptographicOperations.ZeroMemory(macKey);
            }
        }

        private static void DeriveKeys(string password, byte[] salt, int iterations, out byte[] encKey, out byte[] macKey)
        {
            byte[] material = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 64);
            encKey = material.AsSpan(0, 32).ToArray();
            macKey = material.AsSpan(32, 32).ToArray();
            CryptographicOperations.ZeroMemory(material);
        }

        private delegate void ChunkHandler(ReadOnlySpan<byte> chunk);

        private static void CopyRange(Stream input, long count, ChunkHandler handler)
        {
            byte[] buffer = new byte[81920];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = input.Read(buffer, 0, toRead);
                if (read <= 0)
                {
                    throw new BackupPasswordException(WrongPasswordMessage);
                }
                handler(buffer.AsSpan(0, read));
                remaining -= read;
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt32LittleEndian(byte[] buffer, int offset) =>
            buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);

        /// <summary>يمرّر كلّ ما يُكتب إلى الملفّ ويضيفه إلى حساب الـHMAC في الوقت نفسه.</summary>
        private sealed class MacTeeStream : Stream
        {
            private readonly Stream _inner;
            private readonly IncrementalHash _hmac;

            public MacTeeStream(Stream inner, IncrementalHash hmac)
            {
                _inner = inner;
                _hmac = hmac;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                _hmac.AppendData(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                _inner.Write(buffer);
                _hmac.AppendData(buffer);
            }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
