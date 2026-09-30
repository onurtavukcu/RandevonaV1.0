using Domain.Common.Attributes;
using Domain.Models.MongoEncriyption;
using MongoDB.Bson.Serialization;
using System.Reflection;
using System.Security.Cryptography;

namespace Data.MongoDataEncryption
{
    public static class MongoEncryptionMapping
    {
        private static readonly object Sync = new();
        private static byte[]? _registeredKeyFingerprint;

        public static void Register(EncryptionSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var keyBytes = CryptoHelper.GetKeyBytes(settings.Key);
            var fingerprint = SHA256.HashData(keyBytes);
            CryptographicOperations.ZeroMemory(keyBytes);

            lock (Sync)
            {
                if (_registeredKeyFingerprint is not null)
                {
                    if (!CryptographicOperations.FixedTimeEquals(_registeredKeyFingerprint, fingerprint))
                        throw new InvalidOperationException("Mongo encryption mappings already use a different key. Restart and migrate data for key changes.");
                    return;
                }

                var mappings = typeof(EncryptedFieldAttribute).Assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                    .Select(t => new
                    {
                        Type = t,
                        Properties = t.GetProperties()
                            .Where(p => p.GetCustomAttribute<EncryptedFieldAttribute>() is not null).ToArray()
                    })
                    .Where(m => m.Properties.Length > 0)
                    .ToArray();

                foreach (var mapping in mappings)
                {
                    if (BsonClassMap.IsClassMapRegistered(mapping.Type))
                        throw new InvalidOperationException(
                            $"Encryption must be registered before Mongo serialization of {mapping.Type.Name}.");
                    if (mapping.Properties.Any(p => p.PropertyType != typeof(string) ||
                        !p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0))
                        throw new InvalidOperationException(
                            $"EncryptedField requires a readable, writable string property on {mapping.Type.Name}.");
                }

                foreach (var mapping in mappings)
                {
                    var classMap = new BsonClassMap(mapping.Type);
                    classMap.AutoMap();
                    foreach (var property in mapping.Properties)
                        classMap.MapMember(property).SetSerializer(new EncryptedStringSerializer(settings.Key));
                    BsonClassMap.RegisterClassMap(classMap);
                }
                _registeredKeyFingerprint = fingerprint;
            }
        }
    }
}
