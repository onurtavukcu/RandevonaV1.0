using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Data.Repositories.BaseRepositories;
using Domain.Entities.Appointment;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.WorkContext;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

public static class TenantChecks
{
    public static async Task<int> RunAsync(string key)
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            count++;
            Console.WriteLine("PASS: " + name);
        }
        async Task Reject(Func<Task> action, string name)
        {
            var rejected = false;
            try { await action(); } catch { rejected = true; }
            Check(rejected, name);
        }
        const string controlName = "RandevonaControlChecks";
        var a = ObjectId.GenerateNewId().ToString();
        var b = ObjectId.GenerateNewId().ToString();
        var a1 = ObjectId.GenerateNewId().ToString();
        var a2 = ObjectId.GenerateNewId().ToString();
        var b1 = ObjectId.GenerateNewId().ToString();
        var userId = ObjectId.GenerateNewId().ToString();
        var dbA = TenantDatabaseNaming.ForTenant(a, "Tenant A", "randevona_dev");
        var dbB = TenantDatabaseNaming.ForTenant(b, "Tenant B", "randevona_dev");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoSettings:TenantDatabasePrefix"] = "randevona_dev",
            ["MongoSettings:ConnectionString"] = "mongodb://127.0.0.1:1",
            ["MongoSettings:DatabaseName"] = controlName,
            ["MongoSettings:StartupTimeoutSeconds"] = "1",
            ["EncryptionSettings:Key"] = key
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppSettings(config);
        services.AddMongoPersistence();
        var fake = new MemoryMongo();
        services.AddSingleton<IMongoClient>(fake.Client);
        Check(services.Count(d => d.ServiceType == typeof(MongoSettings)) == 1, "Settings registered once");
        Check(!services.Any(d => d.ServiceType == typeof(IMongoDatabase)), "No ambiguous global tenant IMongoDatabase");
        Check(services.Single(d => d.ServiceType == typeof(IMongoDbContext)).Lifetime == ServiceLifetime.Scoped,
            "Tenant DB context is scoped");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var control = provider.GetRequiredService<IControlMongoDbContext>();
        Check(control.Database.DatabaseNamespace.DatabaseName == controlName, "Control database is explicit");
        await Reject(() => Task.FromResult(control.GetCollection<Employees>()), "Business collection denied in control context");
        fake.Seed(controlName, new Tenants { Id = a, DatabaseName = dbA, DefaultOrganizationId = a1, ProvisioningStatus = TenantProvisioningStatus.Active });
        fake.Seed(controlName, new Tenants { Id = b, DatabaseName = dbB, DefaultOrganizationId = b1, ProvisioningStatus = TenantProvisioningStatus.Active });
        fake.Seed(controlName, new Users { Id = userId, TenantId = a, NormalizedEmail = "tenant-a@example.invalid",
            Memberships = [new() { OrganizationId = a1 }] });
        fake.Seed(dbA, new Organizations { Id = a1, TenantId = a });
        fake.Seed(dbA, new Organizations { Id = a2, TenantId = a });
        fake.Seed(dbB, new Organizations { Id = b1, TenantId = b });
        var ea = new Employees { TenantId = a, OrganizationId = a1, FirstName = "Ali" };
        var hidden = new Employees { TenantId = a, OrganizationId = a2, FirstName = "Ahmet" };
        var eb = new Employees { TenantId = b, OrganizationId = b1, FirstName = "Mehmet" };
        fake.Seed(dbA, ea); fake.Seed(dbA, hidden); fake.Seed(dbB, eb);

        IServiceScope Scope(string tenant, string selected, bool all, params string[] allowed)
        {
            var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantWorkContextHolder>().Set(
                new TenantWorkContext(tenant, selected, userId, SystemUserRoleType.User, all, allowed));
            return scope;
        }
        using var scopeA = Scope(a, a1, false, a1);
        using var scopeB = Scope(b, b1, true);
        var repoA = scopeA.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>();
        Check((await repoA.GetAllAsync()).Single().Id == ea.Id, "Tenant A only sees its permitted branch");
        Check((await repoB.GetAllAsync()).Single().Id == eb.Id, "Tenant B uses its own database");
        Check(await repoA.GetByIdAsync(eb.Id) is null, "Foreign tenant Id cannot be read");
        Check(await repoA.GetByIdAsync(hidden.Id) is null, "Other branch Id cannot be read");
        IRepository<Employees> asBase = repoA;
        Check(await asBase.CountAsync() == 1, "Base-interface calls retain scope");
        Check(typeof(IRepository<Employees>).GetMethod("GetCollection") is null, "Raw collection escape removed");
        await Reject(() => scopeA.ServiceProvider.GetRequiredService<IRepository<Employees>>().GetAllAsync(),
            "Unscoped business repository cannot use the control database");
        using (var missing = provider.CreateScope())
            await Reject(() => missing.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>().GetAllAsync(),
                "Missing context fails closed");
        using (var denied = Scope(a, a2, false, a1))
            await Reject(() => denied.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>().GetAllAsync(),
                "Forged selected branch rejected");
        using (var none = Scope(a, "", false))
            Check(await none.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>().CountAsync() == 0,
                "Empty branch membership grants no branch access");
        using (var owner = Scope(a, "", true))
            Check(await owner.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>().CountAsync() == 2,
                "Tenant-wide context can report across its own branches");

        var resolver = scopeA.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
        Check((await resolver.ResolveAsync(userId, a, null, default)).OrganizationId == a1, "Login context selects an authorized default branch");
        await Reject(() => resolver.ResolveAsync(userId, a, b1, default), "Resolver rejects foreign branch");
        await Reject(() => resolver.ResolveAsync(userId, b, b1, default), "Resolver rejects user/tenant mismatch");
        var route = fake.Rows(controlName, nameof(Tenants)).Single(x => x["_id"] == ObjectId.Parse(a));
        route["DatabaseName"] = dbB;
        await Reject(() => repoA.GetAllAsync(), "Corrupted tenant DB mapping rejected");
        route["DatabaseName"] = dbA;
        route["ProvisioningStatus"] = (int)TenantProvisioningStatus.Pending;
        await Reject(() => repoA.GetAllAsync(), "Unprovisioned tenant cannot access business data");
        route["ProvisioningStatus"] = (int)TenantProvisioningStatus.Active;
        var tenantParallel = await Task.WhenAll(repoA.CountAsync(), repoB.CountAsync());
        Check(tenantParallel.SequenceEqual(new long[] { 1, 1 }), "Concurrent request scopes remain separate");
        var created = await repoA.CreateAsync(new Employees { FirstName = "New", IsActive = false });
        Check(created.TenantId == a && created.OrganizationId == a1 && !created.IsActive, "Insert sets ownership without resetting state");
        var originalCreatedAt = created.CreatedAt;
        created.CreatedAt = DateTime.UtcNow.AddYears(1);
        Check(await repoA.UpdateAsync(created), "Scoped update succeeds");
        var persisted = await repoA.GetByIdAsync(created.Id);
        Check(persisted is not null && !persisted.IsActive &&
            Math.Abs((persisted.CreatedAt - originalCreatedAt).TotalMilliseconds) < 1, "Update preserves CreatedAt and inactive state");
        created.OrganizationId = a2;
        await Reject(() => repoA.UpdateAsync(created), "Update cannot move employee to another branch");
        await Reject(() => repoA.UpdateManyAsync(x => true, Builders<Employees>.Update.Set(x => x.TenantId, b)),
            "Bulk update cannot rewrite TenantId");
        await Reject(() => repoA.UpdateManyAsync(x => true, Builders<Employees>.Update.Set(x => x.OrganizationId, a2)),
            "Bulk update cannot rewrite OrganizationId");
        await Reject(() => repoA.UpdateManyAsync(x => true,
            new BsonDocumentUpdateDefinition<Employees>(new BsonDocument("$rename", new BsonDocument("FirstName", "TenantId")))),
            "Bulk rename cannot bypass scope protection");
        var beforeBatch = fake.Rows(dbA, nameof(Employees)).Count;
        await Reject(() => repoA.CreateManyAsync([new Employees(), new Employees { TenantId = b }]),
            "Mixed-tenant batch rejected before any write");
        Check(fake.Rows(dbA, nameof(Employees)).Count == beforeBatch, "Rejected batch writes nothing");
        await Reject(() => repoA.GetAllAsync(0), "Zero unlimited limit rejected");
        await Reject(() => repoA.GetPagedAsync(0, 10), "Invalid page rejected");
        Check(await repoA.DeleteAsync(ea.Id), "Scoped soft delete succeeds");
        Check(fake.Rows(dbA, nameof(Employees)).Any(x => x["_id"] == ObjectId.Parse(ea.Id) && x["IsDeleted"].AsBoolean),
            "Delete preserves the stored document");
        Check(await repoA.GetByIdAsync(ea.Id) is null, "Deleted record hidden");
        await Reject(() => repoA.UpsertAsync(ea), "Upsert cannot resurrect deleted Id");
        Check(await repoA.DeleteAsync(eb.Id) == false, "Foreign tenant delete matches nothing");
        var rogue = new Employees { TenantId = b, OrganizationId = a1 };
        fake.Seed(dbA, rogue);
        Check(await repoA.GetByIdAsync(rogue.Id) is null, "Tenant filter rejects misfiled records even inside a dedicated DB");
        var upserted = new Employees { FirstName = "Upsert" };
        await repoA.UpsertAsync(upserted);
        Check((await repoA.GetByIdAsync(upserted.Id))?.FirstName == "Upsert", "New upsert stays in selected tenant and branch");
        Check(await repoA.UpdateManyAsync(x => x.Id == upserted.Id, Builders<Employees>.Update.Set(x => x.IsActive, false)) == 1,
            "Safe scoped bulk update succeeds");
        using (var owner = Scope(a, "", true))
        {
            var ownerRepo = owner.ServiceProvider.GetRequiredService<IScopedRepository<Employees>>();
            var moved = new Employees { Id = upserted.Id, TenantId = a, OrganizationId = a2 };
            Check(!await ownerRepo.UpdateAsync(moved), "Even tenant-wide update cannot relocate an existing record");
        }
        var branchRepo = scopeA.ServiceProvider.GetRequiredService<IScopedRepository<Organizations>>();
        Check((await branchRepo.GetAllAsync()).Single().Id == a1, "Organization records use their own Id for branch scope");

        var initializer = provider.GetServices<IHostedService>().Single();
        await initializer.StartAsync(default);
        Check(fake.Indexes.Count == 5 && fake.Indexes.All(x => x.Db == controlName) &&
            fake.Indexes.Any(x => x.Name == "ux_ProviderNumberDirectory_PhoneNumberId"),
            "Startup prepares only control indexes");
        fake.FailPing = true;
        await Reject(() => initializer.StartAsync(default), "Control DB failure stops startup");
        fake.FailPing = false;
        fake.DelayPing = true;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await Reject(() => initializer.StartAsync(default), "Startup timeout stops startup");
        Check(timer.Elapsed < TimeSpan.FromSeconds(5), "Startup deadline is bounded");
        fake.DelayPing = false;

        using var provisionScope = provider.CreateScope();
        var provision = provisionScope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        var newTenant = new Tenants { OwnerUserId = userId, CompanyInfos = new() { CompanyName = "Örnek İşletme" } };
        provision.PrepareNewTenant(newTenant, "Kadıköy");
        fake.Seed(controlName, newTenant);
        fake.FailIndexes = true;
        await Reject(() => provision.ProvisionAsync(newTenant.Id), "Provisioning failure reported");
        var newRoute = fake.Rows(controlName, nameof(Tenants)).Single(x => x["_id"] == ObjectId.Parse(newTenant.Id));
        Check(newRoute["ProvisioningStatus"].AsInt32 == (int)TenantProvisioningStatus.Failed, "Failed provisioning is retryable");
        fake.FailIndexes = false;
        await provision.ProvisionAsync(newTenant.Id);
        Check(newRoute["ProvisioningStatus"].AsInt32 == (int)TenantProvisioningStatus.Active, "Tenant activated only after provisioning");
        var firstBranch = fake.Rows(newTenant.DatabaseName, nameof(Organizations)).Single();
        Check(firstBranch["OrganizationInfos"]["OrganizationName"] == "Kadıköy", "User-supplied first branch name preserved");
        await provision.ProvisionAsync(newTenant.Id);
        Check(fake.Rows(newTenant.DatabaseName, nameof(Organizations)).Count == 1, "Repeated provisioning does not duplicate branch");
        var locked = new Tenants { OwnerUserId = userId, CompanyInfos = new() { CompanyName = "İkinci İşletme" } };
        provision.PrepareNewTenant(locked, "İkinci");
        locked.ProvisioningStatus = TenantProvisioningStatus.Provisioning;
        locked.ProvisioningLeaseUntilUtc = DateTime.UtcNow.AddMinutes(1);
        fake.Seed(controlName, locked);
        await Reject(() => provision.ProvisionAsync(locked.Id), "Active provisioning lease blocks another worker");
        fake.Rows(controlName, nameof(Tenants)).Single(x => x["_id"] == ObjectId.Parse(locked.Id))["ProvisioningLeaseUntilUtc"]
            = new BsonDateTime(DateTime.UtcNow.AddMinutes(-1));
        await provision.ProvisionAsync(locked.Id);
        Check(fake.Rows(locked.DatabaseName, nameof(Organizations)).Count == 1, "Expired provisioning lease can resume");
        return count;
    }
}

