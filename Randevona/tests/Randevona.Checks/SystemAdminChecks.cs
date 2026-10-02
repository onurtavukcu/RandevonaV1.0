using CommonServices.Authorization;
using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.Extensions;
using IdentityService.LoginService;
using IdentityService.PasswordService;
using IdentityService.Seeders;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public static class SystemAdminChecks
{
    public static async Task<int> RunAsync(string encryptionKey)
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
            try { await action(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, name);
        }
        const string db = "AdminChecks";
        var fake = new MemoryMongo();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoSettings:ConnectionString"] = "mongodb://127.0.0.1:1", ["MongoSettings:DatabaseName"] = db,
            ["MongoSettings:TenantDatabasePrefix"] = "randevona_dev", ["EncryptionSettings:Key"] = encryptionKey,
            ["JwtSettings:Key"] = new string('k', 48), ["JwtSettings:Issuer"] = "checks",
            ["JwtSettings:Audience"] = "checks", ["JwtSettings:DurationInMinutes"] = "30",
            ["SystemAdminSettings:Emails:0"] = " Admin@Example.Invalid ",
            ["SystemAdminSettings:Emails:1"] = "ADMIN@example.invalid",
            ["SystemAdminSettings:DefaultPassword"] = "seed-password"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppSettings(configuration);
        services.AddMongoPersistence();
        services.AddIdentityServices();
        services.AddSingleton<IMongoClient>(fake.Client);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var settings = provider.GetRequiredService<SystemAdminSettings>();
        var seed = scope.ServiceProvider.GetRequiredService<SystemAdminSeeder>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var login = scope.ServiceProvider.GetRequiredService<ILoginService>();
        int UsersCount() => fake.Rows(db, nameof(Users)).Count;
        Users FindUser(string email) => BsonSerializer.Deserialize<Users>(fake.Rows(db, nameof(Users)).Single(x => x["NormalizedEmail"] == email));
        Check(settings.Emails.Length == 2 && settings.FirstName == "System", "Admin settings bind without tenant or organization configuration");
        var startupServices = provider.GetServices<IHostedService>().ToArray();
        Check(startupServices[0] is MongoIndexInitializerHostedService && startupServices[1] is SystemAdminInitializerHostedService,
            "Mongo indexes precede administrator initialization");
        foreach (var service in startupServices) await service.StartAsync(default);
        var admin = FindUser("ADMIN@EXAMPLE.INVALID");
        Check(UsersCount() == 1 && fake.Rows(db, nameof(Tenants)).Count == 0, "Startup creates one central admin and no tenant");
        Check(fake.Documents.Keys.All(x => x.Db == db) && fake.TransactionCount == 0, "Admin bootstrap needs no tenant database or multi-document transaction");
        Check(admin.SystemRole == SystemUserRoleType.SuperAdmin && admin.UserStatus == UserStatus.Active && admin.IsActive && !admin.IsDeleted,
            "Bootstrap creates an active SuperAdmin");
        Check(admin.TenantId is null && !admin.HasAllOrganizationAccess && admin.Memberships.Count == 0, "Platform admin has no tenant or branch membership");
        Check(admin.FirstName == "System" && admin.LastName == "Administrator" && passwords.VerifyPassword("seed-password", admin.PasswordHash),
            "Bootstrap stores profile and hashed password");
        var result = await login.LoginAsync(new() { Email = admin.Email, Password = "seed-password" });
        Check(result.IsSuccess && result.Value!.Role == "SuperAdmin" && result.Value.TenantId is null && result.Value.OrganizationId is null,
            "Administrator signs in without tenant or organization");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!.Token);
        Check(!jwt.Claims.Any(x => x.Type is "tenantId" or "organizationId"), "Admin JWT contains no tenant or organization claims");
        Check(fake.Documents.Keys.All(x => x.Db == db), "Admin login accesses only the central database");
        var original = fake.Rows(db, nameof(Users)).Single().DeepClone();
        settings.DefaultPassword = "another-password"; settings.FirstName = "Changed";
        await seed.SeedAsync(settings);
        Check(UsersCount() == 1 && original.Equals(fake.Rows(db, nameof(Users)).Single()), "Repeated startup preserves password and profile");
        settings.Emails = ["second@example.invalid"];
        await seed.SeedAsync(settings);
        Check(UsersCount() == 2 && FindUser("SECOND@EXAMPLE.INVALID").TenantId is null && fake.Rows(db, nameof(Tenants)).Count == 0,
            "Additional admin does not create a system tenant");

        var ordinary = new Users { Email = "customer@example.invalid", NormalizedEmail = "CUSTOMER@EXAMPLE.INVALID", UserStatus = UserStatus.Active,
            PasswordHash = passwords.HashPassword("secret") };
        fake.Seed(db, ordinary);
        settings.Emails = [ordinary.Email]; settings.DefaultPassword = "";
        await seed.SeedAsync(settings);
        Check(FindUser(ordinary.NormalizedEmail).SystemRole == SystemUserRoleType.User, "Configured customer email never grants SuperAdmin role");
        Check((await login.LoginAsync(new() { Email = ordinary.Email, Password = "secret" })).Error?.Code == "Login.TenantUnavailable",
            "Customer login still requires a tenant");
        var secondRow = fake.Rows(db, nameof(Users)).Single(x => x["NormalizedEmail"] == "SECOND@EXAMPLE.INVALID");
        secondRow["IsDeleted"] = true;
        settings.Emails = ["second@example.invalid"];
        await seed.SeedAsync(settings);
        Check(UsersCount() == 3 && FindUser("SECOND@EXAMPLE.INVALID").IsDeleted, "Deleted admin is not reactivated");
        settings.Emails = ["new@example.invalid"];
        await Reject(() => seed.SeedAsync(settings), "Missing password prevents new admin creation");
        Check(UsersCount() == 3, "Invalid settings write nothing");

        var legacyTenant = new Tenants { OwnerUserId = admin.Id, DatabaseName = "randevona_dev_legacy" };
        var legacyBranch = new Organizations { TenantId = legacyTenant.Id };
        fake.Seed(db, legacyTenant); fake.Seed(legacyTenant.DatabaseName, legacyBranch);
        var adminRow = fake.Rows(db, nameof(Users)).Single(x => x["NormalizedEmail"] == admin.NormalizedEmail);
        adminRow["TenantId"] = legacyTenant.Id;
        adminRow["HasAllOrganizationAccess"] = true;
        adminRow["Memberships"] = new BsonArray { new UserOrganizationMembership { OrganizationId = legacyBranch.Id }.ToBsonDocument() };
        var oldTenant = fake.Rows(db, nameof(Tenants)).Single().DeepClone();
        var oldBranch = fake.Rows(legacyTenant.DatabaseName, nameof(Organizations)).Single().DeepClone();
        settings.Emails = [admin.Email];
        await seed.SeedAsync(settings);
        var migrated = FindUser(admin.NormalizedEmail);
        Check(migrated.TenantId is null && !migrated.HasAllOrganizationAccess && migrated.Memberships.Count == 0,
            "Configured legacy admin is detached from tenant and memberships");
        Check(migrated.PasswordHash == admin.PasswordHash && migrated.FirstName == admin.FirstName &&
            oldTenant.Equals(fake.Rows(db, nameof(Tenants)).Single()) && oldBranch.Equals(fake.Rows(legacyTenant.DatabaseName, nameof(Organizations)).Single()),
            "Migration preserves identity, old tenant and branch data");

        var resolver = scope.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
        ClaimsPrincipal Identity(string id, string role) => new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role),
            new Claim("tenantId", legacyTenant.Id), new Claim("organizationId", legacyBranch.Id)], "Cookies"));
        var reached = false;
        var middleware = new TenantWorkContextMiddleware(_ => { reached = true; return Task.CompletedTask; });
        async Task<int> Invoke(ClaimsPrincipal identity, bool management = true, bool sharedPage = false, string method = "GET")
        {
            reached = false;
            var context = new DefaultHttpContext { User = identity };
            context.Request.Method = method;
            context.Request.Headers["X-Org-Id"] = legacyBranch.Id;
            if (management) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new PlatformAdminAttribute()), "management"));
            else if (sharedPage) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new WorkspacePageAttribute()), "workspace"));
            var holder = new TenantWorkContextHolder();
            await middleware.InvokeAsync(context, resolver, holder);
            if (holder.Value is not null) throw new Exception("Platform request acquired tenant context");
            return context.Response.StatusCode;
        }
        var principal = Identity(admin.Id, "SuperAdmin");
        Check(await Invoke(principal) == 200 && reached, "Management permits active admin without creating tenant context from stale claims or headers");
        Check(await Invoke(principal, false) == 403 && !reached, "Admin cannot use tenant endpoints through legacy scope claims");
        Check(await Invoke(principal, false, true) == 200 && reached, "Admin can view an explicit shared page without acquiring tenant context");
        Check(await Invoke(principal, false, true, "POST") == 403 && !reached, "Shared page metadata never grants tenantless write access");
        Check(await Invoke(Identity(ordinary.Id, "SuperAdmin"), false, true) == 403 && !reached, "Shared page validates platform role against central account");
        Check(await Invoke(Identity(ordinary.Id, "User"), false, true) == 403 && !reached, "Shared page still requires valid customer tenant context");
        Check(await Invoke(Identity(ordinary.Id, "User")) == 403 && !reached, "Customer cannot enter management");
        Check(await Invoke(Identity(ordinary.Id, "SuperAdmin")) == 403 && !reached, "Stored customer role overrides forged admin claim");
        foreach (var field in new[] { "IsActive", "IsDeleted", "UserStatus", "SystemRole" })
        {
            var previous = adminRow[field];
            adminRow[field] = field switch { "IsActive" => false, "IsDeleted" => true, "UserStatus" => (int)UserStatus.Suspended, _ => (int)SystemUserRoleType.User };
            Check(await Invoke(principal) == 403 && !reached, "Management revokes issued session after changing " + field);
            Check(await Invoke(principal, false, true) == 403 && !reached, "Shared page revokes admin session after changing " + field);
            adminRow[field] = previous;
        }
        settings.DefaultPassword = "seed-password"; settings.Emails = ["retry@example.invalid"];
        fake.FailInsertCollection = nameof(Users);
        await Reject(() => seed.SeedAsync(settings), "Failed admin insert stops initialization");
        Check(UsersCount() == 3, "Failed admin insert leaves no extra account");
        fake.FailInsertCollection = null;
        await seed.SeedAsync(settings);
        Check(UsersCount() == 4 && FindUser("RETRY@EXAMPLE.INVALID").TenantId is null, "Next startup retries account creation without tenant provisioning");
        return count;
    }
}
