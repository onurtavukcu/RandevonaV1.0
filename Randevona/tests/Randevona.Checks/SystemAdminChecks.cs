using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.Extensions;
using IdentityService.LoginService;
using IdentityService.PasswordService;
using IdentityService.Seeders;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

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
            ["SystemAdminSettings:DefaultPassword"] = "seed-password",
            ["SystemAdminSettings:TenantName"] = "System Company", ["SystemAdminSettings:OrganizationName"] = "Operations"
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
        var provision = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        int UsersCount() => fake.Rows(db, nameof(Users)).Count;
        int TenantsCount() => fake.Rows(db, nameof(Tenants)).Count;
        Users FindUser(string normalizedEmail) => BsonSerializer.Deserialize<Users>(fake.Rows(db, nameof(Users))
            .Single(x => x["NormalizedEmail"] == normalizedEmail));
        Tenants FindTenant(string? id) => BsonSerializer.Deserialize<Tenants>(fake.Rows(db, nameof(Tenants)).Single(x => x["_id"] == ObjectId.Parse(id)));
        Check(settings.Emails.Length == 2 && settings.FirstName == "System", "ISettings discovery binds system admin configuration and name defaults");
        var startupServices = provider.GetServices<IHostedService>().ToArray();
        Check(startupServices[0] is MongoIndexInitializerHostedService && startupServices[1] is SystemAdminInitializerHostedService,
            "Mongo startup and indexes run before the system admin initializer");
        foreach (var service in startupServices) await service.StartAsync(default);
        var admin = FindUser("ADMIN@EXAMPLE.INVALID");
        var tenant = FindTenant(admin.TenantId);
        Check(UsersCount() == 1 && TenantsCount() == 1, "Fresh startup creates one admin despite duplicate normalized configured emails");
        Check(admin.SystemRole == SystemUserRoleType.SuperAdmin && admin.UserStatus == UserStatus.Active && admin.IsActive && !admin.IsDeleted,
            "Trusted startup creates an active SuperAdmin");
        Check(admin.FirstName == "System" && admin.LastName == "Administrator" && admin.Email == "Admin@Example.Invalid" &&
            passwords.VerifyPassword("seed-password", admin.PasswordHash), "User model receives normalized identity, names and hashed password");
        Check(tenant.DatabaseName == "randevona_dev_system_company" && tenant.OwnerUserId == admin.Id && tenant.ProvisioningStatus == TenantProvisioningStatus.Active &&
            admin.HasAllOrganizationAccess && admin.Memberships.Single().OrganizationId == tenant.DefaultOrganizationId,
            "Admin tenant and branch membership are ready for the existing login flow");
        var branch = BsonSerializer.Deserialize<Organizations>(fake.Rows(tenant.DatabaseName, nameof(Organizations)).Single());
        Check(branch.OrganizationInfos.OrganizationName == "Operations", "Configured organization name is used for initial branch");
        var login = await scope.ServiceProvider.GetRequiredService<ILoginService>().LoginAsync(new() { Email = admin.Email, Password = "seed-password" });
        Check(login.IsSuccess && login.Value!.Role == "SuperAdmin" && login.Value.TenantId == tenant.Id,
            "Seeded administrator can use the existing login service");
        var original = fake.Rows(db, nameof(Users)).Single().DeepClone();
        settings.DefaultPassword = "another-password";
        settings.FirstName = "Changed";
        await seed.SeedAsync(settings);
        Check(UsersCount() == 1 && TenantsCount() == 1 && original.Equals(fake.Rows(db, nameof(Users)).Single()),
            "Repeated startup never resets existing password, profile or role");
        settings.Emails = ["second@example.invalid"];
        await seed.SeedAsync(settings);
        Check(UsersCount() == 2 && TenantsCount() == 1 && FindUser("SECOND@EXAMPLE.INVALID").TenantId == tenant.Id,
            "Additional configured admin shares the system tenant instead of creating duplicate DB");

        var ordinary = new Users { Email = "customer@example.invalid", NormalizedEmail = "CUSTOMER@EXAMPLE.INVALID", UserStatus = UserStatus.PendingApproval };
        fake.Seed(db, ordinary);
        settings.Emails = [ordinary.Email]; settings.DefaultPassword = "";
        await seed.SeedAsync(settings);
        Check(FindUser(ordinary.NormalizedEmail).SystemRole == SystemUserRoleType.User && FindUser(ordinary.NormalizedEmail).UserStatus == UserStatus.PendingApproval,
            "Configured email never elevates an existing customer or approves their account");
        var secondRow = fake.Rows(db, nameof(Users)).Single(x => x["NormalizedEmail"] == "SECOND@EXAMPLE.INVALID");
        secondRow["IsDeleted"] = true;
        settings.Emails = ["second@example.invalid"];
        await seed.SeedAsync(settings);
        Check(UsersCount() == 3 && FindUser("SECOND@EXAMPLE.INVALID").IsDeleted, "Deleted administrator is not recreated or reactivated");
        settings.Emails = ["new@example.invalid"];
        await Reject(() => seed.SeedAsync(settings), "Missing password blocks new admin creation");
        Check(UsersCount() == 3, "Invalid new account settings write nothing");

        var customerTenant = new Tenants { OwnerUserId = ordinary.Id, CompanyInfos = new() { CompanyName = "Customer Business" } };
        provision.PrepareNewTenant(customerTenant, "Branch");
        fake.Seed(db, customerTenant);
        settings.TenantName = "Customer Business"; settings.DefaultPassword = "seed-password";
        await Reject(() => seed.SeedAsync(settings), "Bootstrap cannot attach an administrator to a customer's existing database");
        Check(UsersCount() == 3, "Customer database collision does not leave a new admin");
        settings.TenantName = "Retry System"; settings.Emails = ["retry-admin@example.invalid"];
        fake.FailIndexes = true;
        await Reject(() => seed.SeedAsync(settings), "Tenant preparation failure fails admin initialization");
        var retryAdmin = FindUser("RETRY-ADMIN@EXAMPLE.INVALID");
        Check(FindTenant(retryAdmin.TenantId).ProvisioningStatus == TenantProvisioningStatus.Failed, "Failed provisioning remains recoverable in central DB");
        fake.FailIndexes = false;
        var beforeRetryUsers = UsersCount(); var beforeRetryTenants = TenantsCount();
        await seed.SeedAsync(settings);
        Check(UsersCount() == beforeRetryUsers && TenantsCount() == beforeRetryTenants &&
            FindTenant(retryAdmin.TenantId).ProvisioningStatus == TenantProvisioningStatus.Active, "Next startup repairs the existing tenant without duplicate accounts");
        settings.Emails = ["rollback-admin@example.invalid"]; settings.TenantName = "Rollback System";
        fake.FailInsertCollection = nameof(Tenants);
        await Reject(() => seed.SeedAsync(settings), "Failed central tenant insert stops system admin creation");
        Check(UsersCount() == beforeRetryUsers && TenantsCount() == beforeRetryTenants, "Bootstrap transaction failure leaves no orphaned admin");
        fake.FailInsertCollection = null;
        return count;
    }
}
