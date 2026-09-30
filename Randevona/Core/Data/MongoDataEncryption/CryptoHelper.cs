using System.Security.Cryptography;
using System.Text;

namespace Data.MongoDataEncryption
{
    public static class CryptoHelper
    {
        private const string Prefix = "gcm:v1:";
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("Randevona:MongoField:gcm:v1");

        internal static byte[] GetKeyBytes(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
                throw new ArgumentException("EncryptionSettings:Key must contain at least 32 UTF-8 bytes; use a randomly generated secret.");
            return SHA256.HashData(Encoding.UTF8.GetBytes(key));
        }

        public static string Encrypt(string plainText, string key)
        {
            ArgumentNullException.ThrowIfNull(plainText);
            var keyBytes = GetKeyBytes(key);
            try
            {
                var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
                var payload = new byte[NonceSize + TagSize + plaintextBytes.Length];
                var nonce = payload.AsSpan(0, NonceSize);
                RandomNumberGenerator.Fill(nonce);
                using var aes = new AesGcm(keyBytes, TagSize);
                aes.Encrypt(nonce, plaintextBytes,
                    payload.AsSpan(NonceSize + TagSize),
                    payload.AsSpan(NonceSize, TagSize), AssociatedData);
                return Prefix + Convert.ToBase64String(payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyBytes);
            }
        }

        public static string Decrypt(string encryptedText, string key)
        {
            ArgumentNullException.ThrowIfNull(encryptedText);
            if (!encryptedText.StartsWith(Prefix, StringComparison.Ordinal))
                throw new CryptographicException("Unsupported encrypted field format. Plaintext and legacy CBC values require an explicit migration.");

            var payload = Convert.FromBase64String(encryptedText[Prefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
                throw new CryptographicException("Encrypted field payload is incomplete.");

            var keyBytes = GetKeyBytes(key);
            try
            {
                var plaintextBytes = new byte[payload.Length - NonceSize - TagSize];
                using var aes = new AesGcm(keyBytes, TagSize);
                aes.Decrypt(payload.AsSpan(0, NonceSize),
                    payload.AsSpan(NonceSize + TagSize),
                    payload.AsSpan(NonceSize, TagSize),
                    plaintextBytes, AssociatedData);
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyBytes);
            }
        }
    }
}
