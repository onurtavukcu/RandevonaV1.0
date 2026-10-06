using BussinessServices.Extensions;
using BussinessServices.ProviderService;
using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Identity.UserEntity;
using Domain.Entities.Whatsapp;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Meta;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public static class ConnectionChecks
{
    public static async Task<int> RunAsync(string key)
    {
        int count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        const string db = "ConnectionChecks";
        var fake = new MemoryMongo();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["MongoSettings:ConnectionString"]="mongodb://127.0.0.1:1", ["MongoSettings:DatabaseName"]=db,
            ["MongoSettings:TenantDatabasePrefix"]="randevona_dev", ["EncryptionSettings:Key"]=key
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddAppSettings(config);
        services.AddMongoPersistence(); services.AddBussinessServices(); services.AddSingleton<IMongoClient>(fake.Client);
        var meta = new ConnectionMetaFake();
        services.AddSingleton<IntegrationServices.Whatsapp.IMetaPhoneClient>(meta);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes=true, ValidateOnBuild=true });
        await MongoIndexes.EnsureControlAsync(fake.Database(db), default);
        using var setup = provider.CreateScope();
        var provision = setup.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        async Task<(Tenants Tenant, Users User)> Business(string name)
        {
            var user = new Users { Email=name+"@example.invalid", NormalizedEmail=name.ToUpperInvariant()+"@EXAMPLE.INVALID", HasAllOrganizationAccess=true };
            var tenant = new Tenants { OwnerUserId=user.Id, CompanyInfos=new(){CompanyName=name} };
            provision.PrepareNewTenant(tenant, "North"); user.TenantId=tenant.Id;
            fake.Seed(db,user); fake.Seed(db,tenant); await provision.ProvisionAsync(tenant.Id);
            return (tenant,user);
        }
        var a=await Business("Connection A"); var b=await Business("Connection B");
        async Task<IServiceScope> Scope(Tenants tenant, Users user, bool admin=false)
        {
            var scope=provider.CreateScope();
            var resolver=scope.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
            var context=admin ? await resolver.ResolveAdminAsync(user.Id, tenant.Id, tenant.DefaultOrganizationId, default)
                : await resolver.ResolveAsync(user.Id,tenant.Id,tenant.DefaultOrganizationId,default);
            scope.ServiceProvider.GetRequiredService<TenantWorkContextHolder>().Set(context);
            return scope;
        }
        using var scopeA=await Scope(a.Tenant,a.User); using var scopeB=await Scope(b.Tenant,b.User);
        var serviceA=scopeA.ServiceProvider.GetRequiredService<IProviderService>(); var serviceB=scopeB.ServiceProvider.GetRequiredService<IProviderService>();
        SaveConnectionRequest Request(Tenants tenant, long version=0, string phoneId="1234567890") => new()
        {
            ExpectedTenantId=tenant.Id, ExpectedOrganizationId=tenant.DefaultOrganizationId, ExpectedVersion=version,
            PhoneNumberId=phoneId, WabaId="2345678901", MetaAppId="3456789012", PhoneNumber="+905551234567", AccessToken="test-token-do-not-reflect"
        };
        var empty=await serviceA.GetAsync();
        Check(empty.IsSuccess && empty.Value!.Version==0 && empty.Value.Numbers.Count==0, "Connections initially empty in current branch");
        var wrongScope=Request(b.Tenant);
        Check((await serviceA.SaveAsync(wrongScope)).Error?.Code=="Connections.WorkspaceChanged", "Stale or forged workspace rejected before save");
        var invalid=Request(a.Tenant); invalid.AccessToken=null;
        Check(!(await serviceA.SaveAsync(invalid)).IsSuccess, "New connection requires access token");
        invalid=Request(a.Tenant); invalid.PhoneNumberId="https://other-host/";
        Check(!(await serviceA.SaveAsync(invalid)).IsSuccess, "External identifiers accept digits only");
        invalid=Request(a.Tenant); invalid.AccessTokenExpiresAt=DateTime.UtcNow.AddDays(-1);
        Check(!(await serviceA.SaveAsync(invalid)).IsSuccess, "Expired new token rejected");
        Check((await serviceA.SaveAsync(Request(a.Tenant))).IsSuccess, "Manual connection saves in current tenant");
        var row=fake.Rows(a.Tenant.DatabaseName,nameof(ProviderData)).Single();
        var encrypted=row["Numbers"].AsBsonArray.Single()["SystemUserAccessToken"].AsString;
        Check(encrypted.StartsWith("gcm:v1:") && !row.ToJson().Contains("test-token-do-not-reflect"), "Nested provider access token encrypted in BSON");
        Check(fake.Rows(db,nameof(ProviderNumberDirectory)).Single()["TenantId"]==a.Tenant.Id &&
            !fake.Rows(db,nameof(ProviderNumberDirectory)).Single().ToJson().Contains("AccessToken"), "Central phone directory contains ownership but no credentials");
        var page=(await serviceA.GetAsync()).Value!;
        Check(page.Version==1 && page.Numbers.Single().Status=="Configured" && page.Numbers.Single().HasAccessToken &&
            !System.Text.Json.JsonSerializer.Serialize(page).Contains("test-token-do-not-reflect"), "Read model omits secrets and never claims Meta verification");
        Check((await serviceB.GetAsync()).Value!.Numbers.Count==0, "Other business cannot see configured number");
        var duplicate=await serviceB.SaveAsync(Request(b.Tenant));
        Check(duplicate.Error?.Type==Domain.Models.Shared.Result.ErrorType.Conflict &&
            fake.Rows(b.Tenant.DatabaseName,nameof(ProviderData)).Count==0, "Global duplicate number rolls back other tenant insert");
        Check((await serviceA.SaveAsync(Request(a.Tenant))).Error?.Type==Domain.Models.Shared.Result.ErrorType.Conflict, "Old version cannot overwrite newer connection");
        var preserve=Request(a.Tenant,1); preserve.AccessToken="";
        Check((await serviceA.SaveAsync(preserve)).IsSuccess &&
            BsonSerializer.Deserialize<ProviderData>(fake.Rows(a.Tenant.DatabaseName,nameof(ProviderData)).Single()).Numbers.Single().SystemUserAccessToken=="test-token-do-not-reflect",
            "Blank edit preserves encrypted token");
        var move=Request(a.Tenant,2); move.WabaId="9999999999";
        Check(!(await serviceA.SaveAsync(move)).IsSuccess, "Account ownership cannot be silently changed");
        var rotate=Request(a.Tenant,2); rotate.AccessToken="replacement-test-token";
        Check((await serviceA.SaveAsync(rotate)).IsSuccess &&
            BsonSerializer.Deserialize<ProviderData>(fake.Rows(a.Tenant.DatabaseName,nameof(ProviderData)).Single()).Numbers.Single().SystemUserAccessToken=="replacement-test-token", "Token replacement persists encrypted credentials");
        fake.FailInsertCollection=nameof(ProviderNumberDirectory);
        var failed=await serviceA.SaveAsync(Request(a.Tenant,3,"4567890123"));
        fake.FailInsertCollection=null;
        Check(!failed.IsSuccess && (await serviceA.GetAsync()).Value!.Version==3 && (await serviceA.GetAsync()).Value!.Numbers.Count==1,
            "Directory failure rolls back tenant update and version");
        Check((await serviceA.SaveAsync(Request(a.Tenant,3,"4567890123"))).IsSuccess, "Rolled-back number can be saved on retry");
        var numberResolver=scopeA.ServiceProvider.GetRequiredService<BussinessServices.ProviderNumberService.IProviderNumberResolver>();
        Check((await numberResolver.ResolveAsync(a.Tenant.Id,a.Tenant.DefaultOrganizationId,null)).Error?.Code=="Provider.NumberRequired", "Multiple WhatsApp numbers require explicit selection");
        Check((await numberResolver.ResolveAsync(a.Tenant.Id,a.Tenant.DefaultOrganizationId,"4567890123")).IsSuccess, "Number resolver selects exact configured number");
        Check((await numberResolver.ResolveAsync(b.Tenant.Id,b.Tenant.DefaultOrganizationId,"4567890123")).Error?.Code=="Provider.ScopeMismatch", "Number resolver cannot cross tenant scope");
        var parallel=await Task.WhenAll(serviceA.SaveAsync(Request(a.Tenant,4,"5678901234")), serviceA.SaveAsync(Request(a.Tenant,4,"6789012345")));
        Check(parallel.Count(x=>x.IsSuccess)==1 && (await serviceA.GetAsync()).Value!.Version==5, "Concurrent forms have exactly one successful version update");
        var admin=new Users {SystemRole=SystemUserRoleType.SuperAdmin,Email="admin@example.invalid",NormalizedEmail="ADMIN@EXAMPLE.INVALID"}; fake.Seed(db,admin);
        using var adminScope=await Scope(a.Tenant,admin,true);
        var adminService=adminScope.ServiceProvider.GetRequiredService<IProviderService>();
        Check((await adminService.SaveAsync(Request(a.Tenant,5,"7890123456"))).IsSuccess &&
            fake.Rows(a.Tenant.DatabaseName,nameof(ProviderData)).Single()["UpdatedByUserId"]==admin.Id, "Selected administrator writes with own audit identity");
        VerifyConnectionRequest Verify(long version) => new() { ExpectedTenantId=a.Tenant.Id, ExpectedOrganizationId=a.Tenant.DefaultOrganizationId, ExpectedVersion=version, PhoneNumberId="1234567890" };
        var verification = await serviceA.CheckAccessAsync(Verify(6));
        Check(verification.IsSuccess && meta.Calls==1 && (await serviceA.GetAsync()).Value!.Numbers.Single(x=>x.PhoneNumberId=="1234567890").LastCheckedAt is not null &&
            !BsonSerializer.Deserialize<ProviderData>(fake.Rows(a.Tenant.DatabaseName,nameof(ProviderData)).Single()).Numbers.Single(x=>x.PhoneNumberId=="1234567890").IsRegistered,
            "Meta access check records success without claiming number registration");
        Check(!(await serviceA.CheckAccessAsync(Verify(6))).IsSuccess && meta.Calls==1, "Stale verification form makes no Meta request");
        meta.Result = new Domain.Models.Shared.Result.Error("Meta.AccessDenied","Access denied.",Domain.Models.Shared.Result.ErrorType.Validation);
        Check(!(await serviceA.CheckAccessAsync(Verify(7))).IsSuccess && (await serviceA.GetAsync()).Value!.Version==7, "Failed Meta check does not save a new successful verification");
        meta.Result = new MetaPhoneAccess("1234567890","+905559999999","Test");
        Check(!(await serviceA.CheckAccessAsync(Verify(7))).IsSuccess && (await serviceA.GetAsync()).Value!.Version==7, "Mismatched Meta display number cannot be verified");
        meta.Result = new MetaPhoneAccess("1234567890","+90 555 123 45 67","Test");
        meta.BeforeReturn=async () => { Check((await serviceA.SaveAsync(Request(a.Tenant,7,"9012345678"))).IsSuccess, "Concurrent token/connection edit succeeds while verification is in flight"); };
        Check((await serviceA.CheckAccessAsync(Verify(7))).Error?.Type==Domain.Models.Shared.Result.ErrorType.Conflict && (await serviceA.GetAsync()).Value!.Version==8, "Delayed Meta response cannot overwrite newer connection");
        fake.Rows(db,nameof(Users)).Single(x=>x["_id"]==ObjectId.Parse(admin.Id))["UserStatus"]=(int)UserStatus.Suspended;
        Check((await adminService.GetAsync()).Error?.Code=="Connections.Forbidden", "Revoked admin selection cannot read credentials or connection metadata");
        fake.Rows(a.Tenant.DatabaseName,nameof(Organizations)).Single()["IsActive"]=false;
        Check((await serviceA.SaveAsync(Request(a.Tenant,6,"8901234567"))).Error?.Code=="Connections.Forbidden", "Branch revocation blocks service writes despite existing request context");
        return count;
    }

    private sealed class ConnectionMetaFake : IntegrationServices.Whatsapp.IMetaPhoneClient
    {
        public int Calls;
        public Func<Task>? BeforeReturn;
        public Domain.Models.Shared.Result.Result<MetaPhoneAccess> Result = new MetaPhoneAccess("1234567890","+90 555 123 45 67","Test");
        public async Task<Domain.Models.Shared.Result.Result<MetaPhoneAccess>> CheckAccessAsync(string wabaId,string phoneNumberId,string token,CancellationToken ct=default)
        { Calls++; if(BeforeReturn is not null) await BeforeReturn(); return Result; }
    }
}
