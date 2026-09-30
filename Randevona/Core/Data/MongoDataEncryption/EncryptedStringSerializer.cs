using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Security.Cryptography;

namespace Data.MongoDataEncryption
{
    public class EncryptedStringSerializer : SerializerBase<string?>
    {
        private readonly string _key;

        public EncryptedStringSerializer(string key)
        {
            var keyBytes = CryptoHelper.GetKeyBytes(key);
            CryptographicOperations.ZeroMemory(keyBytes);
            _key = key;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
        {
            if (value is null)
            {
                context.Writer.WriteNull();
                return;
            }
            context.Writer.WriteString(CryptoHelper.Encrypt(value, _key));
        }

        public override string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.GetCurrentBsonType() == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }
            if (context.Reader.GetCurrentBsonType() != BsonType.String)
                throw new BsonSerializationException("Encrypted field must be a string or null.");

            try
            {
                return CryptoHelper.Decrypt(context.Reader.ReadString(), _key);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new BsonSerializationException(
                    "Encrypted field could not be verified or decrypted. Check the key and stored data format.", ex);
            }
        }
    }
}
