using CommonServices.WorkContext.ContextAccessor;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Data.MongoDbContext;

public interface ITenantDatabaseResolver
{
    Task<IMongoDatabase> GetDatabaseAsync(CancellationToken ct = default);
}
public static class TenantDatabaseNaming
{
    public static string ForTenant(string tenantId, string tenantName, string prefix)
    {
        ValidatePrefix(prefix);
        if (!ObjectId.TryParse(tenantId, out _))
            throw new InvalidOperationException("A valid tenant identifier is required.");
        if (string.IsNullOrWhiteSpace(tenantName))
            throw new ArgumentException("Tenant company name is required.", nameof(tenantName));
        var normalized = tenantName.ToLowerInvariant().Replace('ı', 'i').Normalize(NormalizationForm.FormD);
        var letters = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(letters.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        if (slug.Length == 0) slug = "tenant";
        // Uniqueness is enforced by the central Tenants.DatabaseName index, not an ID suffix.
        var maxSlugLength = 63 - prefix.Length - 1;
        if (slug.Length > maxSlugLength) slug = slug[..maxSlugLength].TrimEnd('_');
        return $"{prefix}_{slug}";
    }
    public static void ValidatePrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || prefix.Length > 24 || !Regex.IsMatch(prefix, "\\Arandevona_[a-z0-9]+\\z"))
            throw new InvalidOperationException("MongoSettings:TenantDatabasePrefix must be randevona_<environment> (lowercase letters/digits, at most 24 characters).");
    }
    public static void Validate(Tenants tenant, string controlDatabase, string prefix)
    {
        ValidatePrefix(prefix);
        if (!ObjectId.TryParse(tenant.Id, out var id) || string.IsNullOrEmpty(tenant.DatabaseName) || tenant.DatabaseName == controlDatabase)
            throw new InvalidOperationException("Tenant database mapping is invalid.");
        // Existing databases are not renamed or recreated during login/provisioning.
        if (tenant.DatabaseName == "randevona_t_" + id) return;
        var start = prefix + "_";
        if (tenant.DatabaseName.Length > 63 || !tenant.DatabaseName.StartsWith(start, StringComparison.Ordinal) ||
            tenant.DatabaseName.Length <= start.Length)
            throw new InvalidOperationException("Tenant database mapping is invalid.");
        var slug = tenant.DatabaseName[start.Length..];
        // Validate the stored name: editing CompanyName must not redirect an existing database.
        if (tenant.DatabaseName != ForTenant(tenant.Id, slug, prefix))
            throw new InvalidOperationException("Tenant database mapping is invalid.");
    }

    public static async Task ValidateAsync(Tenants tenant, IControlMongoDbContext control, string prefix, CancellationToken ct)
    {
        Validate(tenant, control.Database.DatabaseNamespace.DatabaseName, prefix);
        // Also fail closed for conflicting legacy/corrupt mappings before opening a tenant database.
        if (await control.GetCollection<Tenants>().Find(x => x.DatabaseName == tenant.DatabaseName && x.Id != tenant.Id).AnyAsync(ct))
            throw new InvalidOperationException("Tenant database is assigned to another tenant.");
    }
}
public sealed class TenantDatabaseResolver : ITenantDatabaseResolver
{
    private readonly IControlMongoDbContext _control;
    private readonly IMongoClient _client;
    private readonly ITenantContextAccessor _accessor;
    private readonly MongoSettings _settings;
    public TenantDatabaseResolver(IControlMongoDbContext control, IMongoClient client, ITenantContextAccessor accessor, MongoSettings settings)
        => (_control, _client, _accessor, _settings) = (control, client, accessor, settings);

    public async Task<IMongoDatabase> GetDatabaseAsync(CancellationToken ct = default)
    {
        var id = _accessor.CurrentWorkContext.TenantId;
        if (!ObjectId.TryParse(id, out _)) throw new UnauthorizedAccessException("Invalid tenant identifier.");
        var tenant = await _control.GetCollection<Tenants>()
            .Find(x => x.Id == id && x.IsActive && !x.IsDeleted &&
                x.ProvisioningStatus == TenantProvisioningStatus.Active).FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException("Tenant is unavailable.");
        await TenantDatabaseNaming.ValidateAsync(tenant, _control, _settings.TenantDatabasePrefix, ct);
        return _client.GetDatabase(tenant.DatabaseName);
    }
}
