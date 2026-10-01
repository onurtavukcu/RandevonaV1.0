using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.Login;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.Extensions;
using IdentityService.LoginService;
using IdentityService.PasswordService;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public static class LoginChecks
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
        const string db = "LoginChecks";
        var fake = new MemoryMongo();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoSettings:TenantDatabasePrefix"] = "randevona_dev",
            ["MongoSettings:ConnectionString"] = "mongodb://127.0.0.1:1",
            ["MongoSettings:DatabaseName"] = db,
            ["EncryptionSettings:Key"] = encryptionKey,
            ["JwtSettings:Key"] = new string('k', 48),
            ["JwtSettings:Issuer"] = "checks", ["JwtSettings:Audience"] = "checks",
            ["JwtSettings:DurationInMinutes"] = "30"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppSettings(config);
        services.AddMongoPersistence();
        services.AddIdentityServices();
        services.AddSingleton<IMongoClient>(fake.Client);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var login = scope.ServiceProvider.GetRequiredService<ILoginService>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var tenant = new Tenants { ProvisioningStatus = TenantProvisioningStatus.Active };
        tenant.DatabaseName = TenantDatabaseNaming.ForTenant(tenant.Id, "Örnek İşletme", "randevona_dev");
        var branch = new Organizations { TenantId = tenant.Id };
        tenant.DefaultOrganizationId = branch.Id;
        var user = new Users
        {
            Email = "owner@example.invalid", NormalizedEmail = "OWNER@EXAMPLE.INVALID",
            TenantId = tenant.Id, PasswordHash = passwords.HashPassword("secret"),
            Memberships = [new() { OrganizationId = branch.Id }]
        };
        fake.Seed(db, tenant); fake.Seed(db, user); fake.Seed(tenant.DatabaseName, branch);
        var userRow = fake.Rows(db, nameof(Users)).Single();
        var tenantRow = fake.Rows(db, nameof(Tenants)).Single();
        LoginRequest Request(string password = "secret") => new() { Email = " Owner@Example.Invalid ", Password = password };

        Check((await login.LoginAsync(new())).Error?.Code == "Login.InvalidCredentials", "Empty login rejected");
        var wrong = await login.LoginAsync(Request("wrong"));
        var unknown = await login.LoginAsync(new() { Email = "unknown@example.invalid", Password = "secret" });
        Check(wrong.Error?.Code == "Login.InvalidCredentials" && wrong.Error?.Message == unknown.Error?.Message,
            "Wrong password and unknown account return the same public error");
        userRow["UserStatus"] = (int)UserStatus.PendingApproval;
        Check((await login.LoginAsync(Request())).Error?.Code == "Login.PendingApproval", "Pending approval cannot sign in");
        Check((await login.LoginAsync(Request("wrong"))).Error?.Code == "Login.InvalidCredentials", "Wrong password does not disclose approval status");
        foreach (var status in new[] { UserStatus.Inactive, UserStatus.Suspended, UserStatus.Rejected, (UserStatus)999 })
        {
            userRow["UserStatus"] = (int)status;
            Check(!(await login.LoginAsync(Request())).IsSuccess, "Login rejects user status " + status);
        }
        userRow["UserStatus"] = (int)UserStatus.Active;
        userRow["IsActive"] = false;
        Check(!(await login.LoginAsync(Request())).IsSuccess, "Inactive account cannot sign in");
        userRow["IsActive"] = true; userRow["IsDeleted"] = true;
        Check((await login.LoginAsync(Request())).Error?.Code == "Login.InvalidCredentials", "Deleted account is excluded by login repository");
        userRow["IsDeleted"] = false;
        tenantRow["IsActive"] = false;
        Check(!(await login.LoginAsync(Request())).IsSuccess, "Inactive tenant cannot sign in");
        tenantRow["IsActive"] = true;
        tenantRow["ProvisioningStatus"] = (int)TenantProvisioningStatus.Pending;
        Check(!(await login.LoginAsync(Request())).IsSuccess, "Unprepared tenant cannot sign in");
        tenantRow["ProvisioningStatus"] = (int)TenantProvisioningStatus.Active;
        tenantRow["DatabaseName"] = "another_tenant_database";
        Check(!(await login.LoginAsync(Request())).IsSuccess, "Invalid tenant DB mapping cannot obtain a token");
        tenantRow["DatabaseName"] = tenant.DatabaseName;
        var branchRow = fake.Rows(tenant.DatabaseName, nameof(Organizations)).Single();
        branchRow["IsActive"] = false;
        Check(!(await login.LoginAsync(Request())).IsSuccess, "No accessible active branch cannot sign in");
        branchRow["IsActive"] = true;
        var result = await login.LoginAsync(Request());
        Check(result.IsSuccess && result.Value!.TenantId == tenant.Id && result.Value.OrganizationId == branch.Id,
            "Normalized email signs in to the server-validated tenant and branch");
        var response = result.Value!;
        Check(response.ExpiresAtUtc - response.IssuedAtUtc == TimeSpan.FromMinutes(30), "Session expiry follows settings");
        var settings = provider.GetRequiredService<JwtSettings>();
        var handler = new JwtSecurityTokenHandler();
        var validation = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
            ValidateIssuerSigningKey = true, ValidateIssuer = true, ValidIssuer = settings.Issuer,
            ValidateAudience = true, ValidAudience = settings.Audience, ValidateLifetime = true,
            RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = ["at+jwt"]
        };
        var principal = handler.ValidateToken(response.Token, validation, out _);
        Check(principal.FindFirstValue(ClaimTypes.NameIdentifier) == user.Id && principal.FindFirstValue("type") == "access"
            && principal.FindFirstValue("tenantId") == tenant.Id && principal.FindFirstValue("organizationId") == branch.Id
            && principal.IsInRole(SystemUserRoleType.User.ToString()), "Signed access JWT contains the validated account scope");
        var second = await login.LoginAsync(Request());
        Check(handler.ReadJwtToken(response.Token).Id != handler.ReadJwtToken(second.Value!.Token).Id, "Separate logins receive unique JWT ids");
        validation.ValidAudience = "another-app";
        var invalidAudience = false;
        try { handler.ValidateToken(response.Token, validation, out _); } catch (SecurityTokenInvalidAudienceException) { invalidAudience = true; }
        Check(invalidAudience, "Access token rejects another application audience");
        var canceled = false;
        try { await login.LoginAsync(Request(), new CancellationToken(true)); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "Login honors caller cancellation");

        var resolver = scope.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
        var reached = false;
        var middleware = new TenantWorkContextMiddleware(_ => { reached = true; return Task.CompletedTask; });
        async Task<int> Invoke(bool anonymous = false, ClaimsPrincipal? identity = null)
        {
            reached = false;
            var context = new DefaultHttpContext { User = identity ?? principal };
            if (anonymous) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "login"));
            await middleware.InvokeAsync(context, resolver, new TenantWorkContextHolder());
            return context.Response.StatusCode;
        }
        Check(await Invoke() == 200 && reached, "Valid session reaches the protected endpoint");
        userRow["UserStatus"] = (int)UserStatus.PendingApproval;
        Check(await Invoke() == 403 && !reached, "Previously issued session loses access after account status changes");
        Check(await Invoke(anonymous: true) == 200 && reached, "Revoked session can still reach anonymous login or logout");
        userRow["UserStatus"] = (int)UserStatus.Active;
        var staleRole = new ClaimsPrincipal(new ClaimsIdentity(principal.Claims.Where(c => c.Type != ClaimTypes.Role)
            .Append(new Claim(ClaimTypes.Role, "SuperAdmin")), "Cookies"));
        Check(await Invoke(identity: staleRole) == 403 && !reached, "Session with outdated role cannot reach protected endpoint");
        Check(await Invoke(identity: new ClaimsPrincipal(new ClaimsIdentity([], "Cookies"))) == 401 && !reached,
            "Authenticated session without account and tenant claims is rejected");
        return count;
    }
}

