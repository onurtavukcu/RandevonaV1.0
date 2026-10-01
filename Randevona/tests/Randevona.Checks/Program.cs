using System.Reflection;
using System.Security.Cryptography;
using Data.MongoDataEncryption;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Appointment;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.MongoEncriyption;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

const string Key = "0123456789abcdef0123456789abcdef-test-key";
const string OtherKey = "fedcba9876543210fedcba9876543210-other-key";
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
void Reject(Action action, string name)
{
    var rejected = false;
    try { action(); } catch { rejected = true; }
    Check(rejected, name);
}

if (args.Contains("--late-map"))
{
    BsonClassMap.LookupClassMap(typeof(Users));
    Reject(() => MongoEncryptionMapping.Register(new EncryptionSettings { Key = Key }),
        "Late encryption mapping fails rather than silently leaving plaintext fields");
    return;
}

var encrypted = CryptoHelper.Encrypt("Çağrı İpek", Key);
Check(CryptoHelper.Decrypt(encrypted, Key) == "Çağrı İpek", "Unicode encryption round trip");
Check(CryptoHelper.Encrypt("Çağrı İpek", Key) != encrypted, "Fresh nonce on repeated encryption");
Check(CryptoHelper.Decrypt(CryptoHelper.Encrypt("", Key), Key) == "", "Empty string preserved");
Reject(() => CryptoHelper.Decrypt(encrypted, OtherKey), "Wrong key rejected");
Reject(() => CryptoHelper.Encrypt("test", "short"), "Short key rejected");
Reject(() => CryptoHelper.Decrypt("plain text", Key), "Plaintext rejected");
Reject(() => CryptoHelper.Decrypt(Convert.ToBase64String(new byte[32]), Key), "Legacy CBC format rejected");
Reject(() => CryptoHelper.Decrypt("gcm:v1:not-base64", Key), "Malformed encoding rejected");
Reject(() => CryptoHelper.Decrypt("gcm:v1:AA==", Key), "Truncated payload rejected");
foreach (var position in new[] { 0, 12, 28 })
{
    var payload = Convert.FromBase64String(encrypted["gcm:v1:".Length..]);
    payload[position] ^= 1;
    Reject(() => CryptoHelper.Decrypt("gcm:v1:" + Convert.ToBase64String(payload), Key),
        "Tampering rejected at payload offset " + position);
}

MongoEncryptionMapping.Register(new EncryptionSettings { Key = Key });
MongoEncryptionMapping.Register(new EncryptionSettings { Key = Key });
Reject(() => MongoEncryptionMapping.Register(new EncryptionSettings { Key = OtherKey }),
    "Changing a registered key in-process rejected");
var user = new Users { FirstName = "Ayşe", LastName = "Öztürk", NormalizedEmail = "test@example.invalid" };
var document = user.ToBsonDocument();
Check(document["FirstName"].AsString.StartsWith("gcm:v1:") &&
    document["LastName"].AsString.StartsWith("gcm:v1:"), "User names encrypted in BSON");
var restored = BsonSerializer.Deserialize<Users>(document);
Check(restored.FirstName == user.FirstName && restored.LastName == user.LastName, "User names deserialize correctly");
Check(document["NormalizedEmail"] == user.NormalizedEmail, "Indexed email remains queryable");
var employeeDoc = new Employees { FirstName = "Ali", LastName = "Veli" }.ToBsonDocument();
Check(employeeDoc["FirstName"].AsString.StartsWith("gcm:v1:") &&
    BsonSerializer.Deserialize<Employees>(employeeDoc).LastName == "Veli", "Employee encryption mapping works");
var nullDoc = new Users { FirstName = null!, LastName = "" }.ToBsonDocument();
var nullRestored = BsonSerializer.Deserialize<Users>(nullDoc);
Check(nullDoc["FirstName"].IsBsonNull && nullRestored.FirstName is null &&
    nullRestored.LastName == "", "Null and empty strings remain distinct");
document["FirstName"] = "plain text";
Reject(() => BsonSerializer.Deserialize<Users>(document), "Serializer never returns undecryptable input");
document["FirstName"] = 42;
Reject(() => BsonSerializer.Deserialize<Users>(document), "Non-string encrypted field rejected");

Reject(() => new MongoSettings().Validate(), "Missing connection rejected");
Reject(() => new MongoSettings { ConnectionString = "mongodb://localhost" }.Validate(), "Missing database rejected");
Reject(() => new MongoSettings { ConnectionString = "mongodb://localhost", DatabaseName = "Checks",
    StartupTimeoutSeconds = 0 }.Validate(), "Invalid startup timeout rejected");

checks += TenantNamingChecks.Run();
checks += await TenantChecks.RunAsync(Key);
checks += await PasswordChecks.RunAsync();
checks += await RegisterChecks.RunAsync(Key);
checks += await LoginChecks.RunAsync(Key);
checks += await SystemAdminChecks.RunAsync(Key);
Console.WriteLine($"Completed {checks} offline checks; no database connections or writes.");

public class InterfaceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}


