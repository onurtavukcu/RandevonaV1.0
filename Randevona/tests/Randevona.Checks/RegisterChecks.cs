using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Data.Repositories;
using Data.Repositories.Identity.Registration;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.Register;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.Extensions;
using IdentityService.PasswordService;
using IdentityService.RegisterService;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public static class RegisterChecks
{
    public static async Task<int> RunAsync(string encryptionKey)
    {
        var count = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
            count++;
            Console.WriteLine("PASS: " + message);
        }
        const string db = "RegisterChecks";
        var fake = new MemoryMongo();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoSettings:ConnectionString"] = "mongodb://127.0.0.1:1",
            ["MongoSettings:DatabaseName"] = db,
            ["EncryptionSettings:Key"] = encryptionKey,
            ["JwtSettings:Key"] = new string('k', 48),
            ["JwtSettings:Issuer"] = "checks", ["JwtSettings:Audience"] = "checks"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppSettings(configuration);
        services.AddMongoPersistence();
        services.AddIdentityServices();
        services.AddSingleton<IMongoClient>(fake.Client);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var register = scope.ServiceProvider.GetRequiredService<IRegisterService>();
        var repository = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var provision = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await MongoIndexes.EnsureControlAsync(fake.Database(db), default);
        RegisterRequest Request(string email) => new()
        {
            FirstName = " Ayşe ", LastName = " Öztürk ", CompanyName = " Örnek İşletme ",
            OrganizationName = " Kadıköy ", Email = email, Password = "secret", ConfirmPassword = "secret"
        };
        int UsersCount() => fake.Rows(db, nameof(Users)).Count;
        int TenantsCount() => fake.Rows(db, nameof(Tenants)).Count;
        Users FindUser(string email) => BsonSerializer.Deserialize<Users>(fake.Rows(db, nameof(Users))
            .Single(x => x["NormalizedEmail"] == email.Trim().ToUpperInvariant()));
        Tenants FindTenant(string? id) => BsonSerializer.Deserialize<Tenants>(fake.Rows(db, nameof(Tenants))
            .Single(x => x["_id"] == ObjectId.Parse(id)));

        var invalid = Request("bad-address");
        Check(!(await register.RegisterAsync(invalid)).IsSuccess && UsersCount() == 0, "Invalid registration email writes nothing");
        invalid = Request("invalid@example.invalid"); invalid.OrganizationName = " ";
        Check(!(await register.RegisterAsync(invalid)).IsSuccess && TenantsCount() == 0, "Blank branch cannot create tenant");
        invalid = Request("invalid@example.invalid"); invalid.ConfirmPassword = "different";
        Check(!(await register.RegisterAsync(invalid)).IsSuccess, "Registration rejects password mismatch");
        invalid.Password = invalid.ConfirmPassword = new string('ş', 37);
        Check(!(await register.RegisterAsync(invalid)).IsSuccess, "Registration shares BCrypt byte limit");

        var result = await register.RegisterAsync(Request(" Owner@Example.Invalid "));
        Check(result.IsSuccess && result.Value!.IsTenantReady && result.Value.UserStatus == UserStatus.PendingApproval,
            "Registration succeeds but waits for superadmin approval");
        var owner = FindUser("owner@example.invalid");
        var tenant = FindTenant(owner.TenantId);
        Check(owner.SystemRole == SystemUserRoleType.User && owner.UserStatus == UserStatus.PendingApproval,
            "Public registration never creates an active account or superadmin");
        Check(owner.FirstName == "Ayşe" && owner.LastName == "Öztürk" && owner.NormalizedEmail == "OWNER@EXAMPLE.INVALID",
            "Registration trims names and normalizes email");
        Check(passwords.VerifyPassword("secret", owner.PasswordHash) && owner.PasswordHash != "secret", "Registered password is hashed");
        Check(tenant.OwnerUserId == owner.Id && owner.TenantId == tenant.Id && owner.Memberships.Single().OrganizationId == tenant.DefaultOrganizationId,
            "User tenant ownership and initial membership agree");
        Check(tenant.DatabaseName == TenantDatabaseNaming.ForTenant(tenant.Id) && tenant.ProvisioningStatus == TenantProvisioningStatus.Active,
            "Tenant database is prepared independently of approval");
        var branch = BsonSerializer.Deserialize<Organizations>(fake.Rows(tenant.DatabaseName, nameof(Organizations)).Single());
        Check(branch.OrganizationInfos.OrganizationName == "Kadıköy" && branch.TenantId == tenant.Id, "User-named branch is stored in tenant DB");
        Check(fake.TransactionCount == 1 && fake.TransactionInsertCount == 2, "Central user and tenant are inserted with one transaction session");
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
        var denied = false;
        try { await resolver.ResolveAsync(owner.Id, tenant.Id, branch.Id, default); }
        catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "Pending approval user cannot obtain authenticated tenant context");
        Check(!(await register.RegisterAsync(Request("owner@example.invalid"))).IsSuccess && UsersCount() == 1 && TenantsCount() == 1,
            "Repeated registration cannot duplicate user or tenant");

        fake.FailInsertCollection = nameof(Tenants);
        var failed = await register.RegisterAsync(Request("rollback@example.invalid"));
        Check(!failed.IsSuccess && UsersCount() == 1 && TenantsCount() == 1, "Second insert failure rolls back central user as well");
        fake.FailInsertCollection = null;
        fake.FailTransactions = true;
        Check(!(await register.RegisterAsync(Request("no-transaction@example.invalid"))).IsSuccess && UsersCount() == 1,
            "Unavailable transactions do not fall back to partial inserts");
        fake.FailTransactions = false;

        fake.FailIndexes = true;
        var retry = await register.RegisterAsync(Request("retry@example.invalid"));
        var retryUser = FindUser("retry@example.invalid");
        var retryTenant = FindTenant(retryUser.TenantId);
        Check(retry.IsSuccess && !retry.Value!.IsTenantReady && retryTenant.ProvisioningStatus == TenantProvisioningStatus.Failed &&
            retryUser.UserStatus == UserStatus.PendingApproval, "Provisioning failure preserves pending application for admin retry");
        fake.FailIndexes = false;
        await provision.ProvisionAsync(retryTenant.Id);
        Check(FindTenant(retryTenant.Id).ProvisioningStatus == TenantProvisioningStatus.Active && UsersCount() == 2 && TenantsCount() == 2,
            "Provisioning retry repairs same tenant without duplicate central accounts");

        // Force both preliminary email lookups to finish before either attempts the transaction.
        var readers = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> ConcurrentLookup(Task<bool> lookup)
        {
            var exists = await lookup;
            if (Interlocked.Increment(ref readers) == 2) gate.SetResult();
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return exists;
        }
        var racingRepository = InterfaceProxy.Create<IRegistrationRepository>((method, args) =>
        {
            var value = method.Invoke(repository, args);
            return method.Name == nameof(IRegistrationRepository.EmailExistsAsync) ? ConcurrentLookup((Task<bool>)value!) : value;
        });
        var racing = new RegisterService(racingRepository, passwords, provision, NullLogger<RegisterService>.Instance);
        var races = await Task.WhenAll(racing.RegisterAsync(Request("race@example.invalid")), racing.RegisterAsync(Request("RACE@example.invalid")));
        Check(races.Count(x => x.IsSuccess) == 1 && races.Count(x => x.Error?.Code == "Register.EmailExists") == 1 &&
            UsersCount() == 3 && TenantsCount() == 3, "Concurrent same-email registration creates exactly one user and tenant");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelledSafely = false;
        try { await register.RegisterAsync(Request("cancelled@example.invalid"), cancelled.Token); }
        catch (OperationCanceledException) { cancelledSafely = true; }
        Check(cancelledSafely && UsersCount() == 3, "Cancelled registration performs no writes");
        return count;
    }
}
