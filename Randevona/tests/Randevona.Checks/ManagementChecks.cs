using CommonServices.Authorization;
using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Data.MongoDbContext;
using Data.MongoDbContext.MongoExtension;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Management;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.Extensions;
using IdentityService.LoginService;
using IdentityService.ManagementService;
using IdentityService.PasswordService;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public static class ManagementChecks
{
    public static async Task<int> RunAsync(string encryptionKey)
    {
        int count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        const string db = "ManagementChecks";
        var fake = new MemoryMongo();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["MongoSettings:ConnectionString"]="mongodb://127.0.0.1:1", ["MongoSettings:DatabaseName"]=db,
            ["MongoSettings:TenantDatabasePrefix"]="randevona_dev", ["EncryptionSettings:Key"]=encryptionKey,
            ["JwtSettings:Key"]=new string('k',48), ["JwtSettings:Issuer"]="checks", ["JwtSettings:Audience"]="checks", ["JwtSettings:DurationInMinutes"]="30"
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddAppSettings(config);
        services.AddMongoPersistence(); services.AddIdentityServices(); services.AddSingleton<IMongoClient>(fake.Client);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes=true, ValidateOnBuild=true });
        using var scope = provider.CreateScope();
        var management = scope.ServiceProvider.GetRequiredService<IManagementService>();
        var provision = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        var login = scope.ServiceProvider.GetRequiredService<ILoginService>();
        var password = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashPassword("secret");
        var admin = new Users { SystemRole=SystemUserRoleType.SuperAdmin, Email="admin@example.invalid", NormalizedEmail="ADMIN@EXAMPLE.INVALID" };
        fake.Seed(db, admin);
        (Users User, Tenants Tenant) Application(string name)
        {
            var user = new Users { FirstName="Test", LastName=name, Email=name+"@example.invalid", NormalizedEmail=(name+"@example.invalid").ToUpperInvariant(),
                PasswordHash=password, UserStatus=UserStatus.PendingApproval, HasAllOrganizationAccess=true };
            var tenant = new Tenants { OwnerUserId=user.Id, CompanyInfos=new() {CompanyName=name} };
            provision.PrepareNewTenant(tenant,"North Branch"); user.TenantId=tenant.Id;
            fake.Seed(db,user); fake.Seed(db,tenant); return (user,tenant);
        }
        Users Current(Users user) => BsonSerializer.Deserialize<Users>(fake.Rows(db,nameof(Users)).Single(x=>x["_id"]==ObjectId.Parse(user.Id)));
        var application = Application("First Business");
        Check((await management.GetUsersAsync(application.User.Id,new())).Error?.Code=="Management.Forbidden", "Customer cannot call management service directly");
        Check((await management.ReviewAsync(application.User.Id, application.User.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.Forbidden", "Customer cannot approve self");
        Check((await management.GetTenantsAsync(admin.Id,new(){Page=0})).Error?.Code=="Management.InvalidQuery", "Invalid pagination is rejected");
        Check((await management.GetUsersAsync(admin.Id,new(){Status=(UserStatus)99})).Error?.Code=="Management.InvalidQuery", "Invalid status is rejected");
        Check((await management.GetUserAsync(admin.Id,"invalid")).Error?.Code=="Management.NotFound", "Invalid user id returns not found");
        var users = await management.GetUsersAsync(admin.Id,new(){Status=UserStatus.PendingApproval,Search="FIRST"});
        Check(users.IsSuccess && users.Value!.Items.Single().Id==application.User.Id, "Pending user filter and case-insensitive email search work");
        var tenants = await management.GetTenantsAsync(admin.Id,new(){Search="business"});
        Check(tenants.IsSuccess && tenants.Value!.Items.Single().Owner?.Id==application.User.Id, "Tenant search includes owner application status");
        Check((await management.GetTenantsAsync(admin.Id,new(){Search=".*"})).Value!.TotalCount==0, "Search treats regex input as literal text");
        Check((await management.GetTenantAsync(admin.Id,application.Tenant.Id)).Value!.Users.Items.Single().Id==application.User.Id, "Tenant detail only includes that tenant's users");
        Check(!(await login.LoginAsync(new(){Email=application.User.Email,Password="secret"})).IsSuccess, "Pending application cannot sign in");
        var originalHash=Current(application.User).PasswordHash;
        var approved=await management.ReviewAsync(admin.Id,application.User.Id,ApplicationDecision.Approve,null);
        var after=Current(application.User);
        Check(approved.IsSuccess && after.UserStatus==UserStatus.Active && after.ReviewedByUserId==admin.Id && after.ReviewedAtUtc is not null, "Approval atomically saves active status and reviewer audit");
        Check(after.PasswordHash==originalHash && after.SystemRole==SystemUserRoleType.User && after.TenantId==application.Tenant.Id, "Approval preserves password, role and tenant ownership");
        Check((await login.LoginAsync(new(){Email=application.User.Email,Password="secret"})).IsSuccess, "Approved user can sign in after tenant preparation");
        var audit=after.ReviewedAtUtc;
        Check((await management.ReviewAsync(admin.Id,application.User.Id,ApplicationDecision.Reject,"Later change")).Error?.Code=="Management.ReviewConflict" && Current(application.User).ReviewedAtUtc==audit,
            "Stale form cannot overwrite a completed decision or audit");
        Check((await management.GetUserAsync(admin.Id,application.User.Id)).Value!.Reviewer?.Id==admin.Id, "User detail resolves reviewer identity");
        Check((await management.ReviewAsync(admin.Id,admin.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.ReviewConflict", "Platform admin cannot be reviewed or self-approved");

        var rejected=Application("Rejected Business");
        Check((await management.ReviewAsync(admin.Id,rejected.User.Id,ApplicationDecision.Reject,"  ")).Error?.Code=="Management.ReasonRequired", "Rejection requires nonblank reason");
        Check((await management.ReviewAsync(admin.Id,rejected.User.Id,ApplicationDecision.Reject,new string('x',1001))).Error?.Code=="Management.ReasonRequired", "Oversized rejection reason is rejected");
        Check((await management.ReviewAsync(admin.Id,rejected.User.Id,(ApplicationDecision)99,null)).Error?.Code=="Management.InvalidDecision", "Unknown decision cannot alter account state");
        var rejectedResult=await management.ReviewAsync(admin.Id,rejected.User.Id,ApplicationDecision.Reject,"  Business details incomplete.  ");
        Check(rejectedResult.IsSuccess && Current(rejected.User).RejectionReason=="Business details incomplete." && Current(rejected.User).UserStatus==UserStatus.Rejected, "Rejection records trimmed reason and status");
        Check(!fake.Documents.Keys.Any(x=>x.Db==rejected.Tenant.DatabaseName), "Rejection does not create tenant database");
        Check(!(await login.LoginAsync(new(){Email=rejected.User.Email,Password="secret"})).IsSuccess, "Rejected user cannot sign in");
        var failed=Application("Retry Business"); fake.FailIndexes=true;
        Check((await management.ReviewAsync(admin.Id,failed.User.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.ProvisioningFailed" && Current(failed.User).UserStatus==UserStatus.PendingApproval && Current(failed.User).ReviewedAtUtc is null,
            "Failed provisioning preserves pending application without approval audit");
        fake.FailIndexes=false;
        Check((await management.ReviewAsync(admin.Id,failed.User.Id,ApplicationDecision.Approve,null)).IsSuccess && fake.Rows(failed.Tenant.DatabaseName,nameof(Organizations)).Count==1, "Approval retries failed preparation without duplicate branch");
        var disabled=Application("Disabled Business");
        fake.Rows(db,nameof(Tenants)).Single(x=>x["_id"]==ObjectId.Parse(disabled.Tenant.Id))["IsActive"]=false;
        Check((await management.ReviewAsync(admin.Id,disabled.User.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.TenantUnavailable", "Disabled business cannot be approved");
        var branchless=Application("No Branch"); await provision.ProvisionAsync(branchless.Tenant.Id);
        fake.Rows(branchless.Tenant.DatabaseName,nameof(Organizations)).Single()["IsActive"]=false;
        Check((await management.ReviewAsync(admin.Id,branchless.User.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.TenantUnavailable", "Prepared database without an active branch cannot be approved");
        var race=Application("Race Business"); await provision.ProvisionAsync(race.Tenant.Id);
        var results=await Task.WhenAll(management.ReviewAsync(admin.Id,race.User.Id,ApplicationDecision.Approve,null),management.ReviewAsync(admin.Id,race.User.Id,ApplicationDecision.Reject,"Rejected concurrently"));
        Check(results.Count(x=>x.IsSuccess)==1 && results.Count(x=>x.Error?.Code=="Management.ReviewConflict")==1, "Competing approval and rejection produce exactly one decision");
        var second=new Users{Email="employee@example.invalid",NormalizedEmail="EMPLOYEE@EXAMPLE.INVALID",UserStatus=UserStatus.PendingApproval,TenantId=branchless.Tenant.Id,HasAllOrganizationAccess=true}; fake.Seed(db,second);
        Check((await management.ReviewAsync(admin.Id,second.Id,ApplicationDecision.Approve,null)).Error?.Code=="Management.OwnerNotApproved", "Secondary user cannot bypass owner's pending application");
        for(int i=0;i<24;i++) Application("Page "+i);
        var page1=(await management.GetUsersAsync(admin.Id,new())).Value!; var page2=(await management.GetUsersAsync(admin.Id,new(){Page=2})).Value!;
        Check(page1.Items.Count==20 && page1.HasNext && page2.HasPrevious && !page1.Items.Select(x=>x.Id).Intersect(page2.Items.Select(x=>x.Id)).Any(), "User pagination is bounded and pages do not overlap");
        var deleted=Application("Deleted"); fake.Rows(db,nameof(Users)).Single(x=>x["_id"]==ObjectId.Parse(deleted.User.Id))["IsDeleted"]=true;
        Check((await management.GetUserAsync(admin.Id,deleted.User.Id)).Error?.Code=="Management.NotFound", "Deleted account excluded from management details");
        var options = await management.GetWorkspaceAsync(admin.Id, application.Tenant.Id);
        Check(options.IsSuccess && options.Value!.Organizations.Single().Id == application.Tenant.DefaultOrganizationId, "Workspace lists approved business branches");
        Check(!(await management.GetWorkspaceAsync(application.User.Id, application.Tenant.Id)).IsSuccess, "Customer cannot choose a managed workspace");
        Check(!(await management.GetWorkspaceAsync(admin.Id, rejected.Tenant.Id)).IsSuccess, "Rejected owner cannot be managed");
        Check(!(await management.GetWorkspaceAsync(admin.Id, disabled.Tenant.Id)).IsSuccess, "Disabled business cannot be selected");
        Check(!(await management.SelectWorkspaceAsync(admin.Id, application.Tenant.Id, "")).IsSuccess, "Workspace requires an explicit branch");
        Check(!(await management.SelectWorkspaceAsync(admin.Id, application.Tenant.Id, failed.Tenant.DefaultOrganizationId)).IsSuccess, "Foreign branch cannot be selected");
        var selected = (await management.SelectWorkspaceAsync(admin.Id, application.Tenant.Id, application.Tenant.DefaultOrganizationId)).Value!;
        Check(selected.UserId == admin.Id && selected.SystemUserRoleType == SystemUserRoleType.SuperAdmin && !selected.HasAllOrganizationAccess && selected.AllowedOrganizationIds!.SequenceEqual(new[]{application.Tenant.DefaultOrganizationId}), "Selected workspace preserves admin actor and limits branch scope");
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantWorkContextResolver>();
        async Task<(DefaultHttpContext Http, TenantWorkContextHolder Holder, bool Called)> Request(bool selection = true, bool central = false, string method = "GET")
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, admin.Id), new(ClaimTypes.Role, nameof(SystemUserRoleType.SuperAdmin)), new("tenantId", failed.Tenant.Id), new("organizationId", failed.Tenant.DefaultOrganizationId) };
            if (selection) { claims.Add(new(AdminWorkspaceClaims.TenantId, application.Tenant.Id)); claims.Add(new(AdminWorkspaceClaims.OrganizationId, application.Tenant.DefaultOrganizationId)); }
            var http = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "checks")) };
            http.Request.Method = method; http.Request.Headers["X-Org-Id"] = failed.Tenant.DefaultOrganizationId;
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(central ? (object)new PlatformAdminAttribute() : new WorkspacePageAttribute()), "checks"));
            var holder = new TenantWorkContextHolder(); bool called = false;
            await new TenantWorkContextMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(http, resolver, holder);
            return (http, holder, called);
        }
        var scoped = await Request();
        Check(scoped.Called && scoped.Holder.Value?.TenantId == application.Tenant.Id && scoped.Holder.Value.OrganizationId == application.Tenant.DefaultOrganizationId, "Admin selection ignores arbitrary headers and legacy claims");
        var central = await Request(central:true);
        Check(central.Called && central.Holder.Value is null, "Management stays central with a selected business");
        var unselected = await Request(selection:false);
        Check(unselected.Called && unselected.Holder.Value is null, "Headers and normal tenant claims cannot select an admin workspace");
        var deniedPost = await Request(method:"POST");
        Check(!deniedPost.Called && deniedPost.Http.Response.StatusCode == 403, "Workspace page marker never authorizes admin writes");
        var branchRow = fake.Rows(application.Tenant.DatabaseName, nameof(Organizations)).Single(); branchRow["IsActive"] = false;
        var revoked = await Request();
        Check(!revoked.Called && revoked.Holder.Value is null && revoked.Http.Response.StatusCode == 302 && revoked.Http.Response.Headers.Location.ToString().Contains("selectionUnavailable=true"), "Revoked branch redirects without silently switching workspace");
        branchRow["IsActive"] = true;
        fake.Rows(db,nameof(Users)).Single(x=>x["_id"]==ObjectId.Parse(admin.Id))["UserStatus"]=(int)UserStatus.Suspended;
        Check((await management.GetTenantsAsync(admin.Id,new())).Error?.Code=="Management.Forbidden", "Suspended administrator loses service access");
        return count;
    }
}
