using Data.MongoDbContext;
using Domain.Entities.Identity.UserEntity;
using MongoDB.Bson;

public static class TenantNamingChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            count++;
            Console.WriteLine("PASS: " + name);
        }
        void Reject(Action action, string name)
        {
            var rejected = false;
            try { action(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, name);
        }
        var id = ObjectId.GenerateNewId().ToString();
        var otherId = ObjectId.GenerateNewId().ToString();
        var name = TenantDatabaseNaming.ForTenant(id, "  İstanbul Şişli Çığ  ", "randevona_dev");
        Check(name == "randevona_dev_istanbul_sisli_cig", "Tenant DB name exposes environment and normalized company name without ID suffix");
        Check(name.Length <= 63 && name.All(c => c < 128), "Tenant DB name stays under the ASCII byte limit");
        Check(TenantDatabaseNaming.ForTenant(id, "Test", "randevona_dev") == "randevona_dev_test",
            "Short company name stays fully readable");
        Check(TenantDatabaseNaming.ForTenant(id, "Onur", "randevona_dev") == TenantDatabaseNaming.ForTenant(otherId, "Onur", "randevona_dev"),
            "Database naming never appends an ID to distinguish identical names");
        Check(TenantDatabaseNaming.ForTenant(id, "Onur", "randevona_dev") != TenantDatabaseNaming.ForTenant(id, "Onur", "randevona_prod"),
            "Same tenant in development and production uses separate database names");
        Check(TenantDatabaseNaming.ForTenant(id, "公司", "randevona_dev") == "randevona_dev_tenant",
            "Unsupported alphabet has a canonical fallback subject to database uniqueness");
        Check(TenantDatabaseNaming.ForTenant(id, "../ACME:$\" \\ / LTD", "randevona_dev") == "randevona_dev_acme_ltd",
            "Punctuation and database-invalid characters cannot enter the database name");
        Check(TenantDatabaseNaming.ForTenant(id, new string('a', 200), "randevona_production").Length == 63,
            "Long names adapt to the environment prefix length");
        var tenant = new Tenants { Id = id, DatabaseName = name, CompanyInfos = new() { CompanyName = "A new business name" } };
        TenantDatabaseNaming.Validate(tenant, "Control", "randevona_dev");
        Check(tenant.DatabaseName == name, "Renaming displayed company does not remap stored database");
        Reject(() => TenantDatabaseNaming.Validate(tenant, "Control", "randevona_prod"), "Mapping from another environment is rejected");
        Reject(() => TenantDatabaseNaming.Validate(tenant, name, "randevona_dev"), "Tenant cannot use the central database");
        tenant.Id = "invalid-id";
        Reject(() => TenantDatabaseNaming.Validate(tenant, "Control", "randevona_dev"), "Invalid tenant identity rejected");
        tenant.Id = id; tenant.DatabaseName = "randevona_dev_UPPER_" + id;
        Reject(() => TenantDatabaseNaming.Validate(tenant, "Control", "randevona_dev"), "Noncanonical saved names are rejected");
        tenant.DatabaseName = "randevona_t_" + id;
        TenantDatabaseNaming.Validate(tenant, "Control", "randevona_dev");
        Check(tenant.DatabaseName == "randevona_t_" + id, "Existing legacy mapping remains accessible without data migration");
        tenant.Id = otherId;
        Reject(() => TenantDatabaseNaming.Validate(tenant, "Control", "randevona_dev"), "Legacy compatibility still enforces tenant id");
        Reject(() => TenantDatabaseNaming.ForTenant(id, "Onur", "randevona_dev/"), "Unsafe environment prefix rejected");
        Reject(() => new MongoSettings { ConnectionString = "mongodb://localhost", DatabaseName = "Control" }.Validate(),
            "Missing environment prefix fails configuration validation");
        return count;
    }
}
