namespace Domain.Models.Meta;

// Read models intentionally contain no credentials.
public sealed record ConnectionNumber(string PhoneNumberId, string WabaId, string PhoneNumber,
    string MetaAppId, string Status, bool HasAccessToken, DateTime? AccessTokenExpiresAt, DateTime? LastCheckedAt);
public sealed record ConnectionPage(string TenantId, string OrganizationId, long Version,
    IReadOnlyList<ConnectionNumber> Numbers);

public sealed class SaveConnectionRequest
{
    public string ExpectedTenantId { get; set; } = "";
    public string ExpectedOrganizationId { get; set; } = "";
    public long ExpectedVersion { get; set; }
    public string PhoneNumberId { get; set; } = "";
    public string WabaId { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string MetaAppId { get; set; } = "";
    public string? AccessToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
}
