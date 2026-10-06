using Domain.Models.Shared.Settings;
namespace Domain.Models.Meta;

public sealed class WhatsAppApiSettings : ISettings
{
    // Set the version enabled for your own Meta application. Blank disables API calls.
    public string GraphApiVersion { get; set; } = "";
}
public sealed record MetaPhoneAccess(string PhoneNumberId, string DisplayPhoneNumber, string? VerifiedName);
public sealed class VerifyConnectionRequest
{
    public string ExpectedTenantId { get; set; } = "";
    public string ExpectedOrganizationId { get; set; } = "";
    public long ExpectedVersion { get; set; }
    public string PhoneNumberId { get; set; } = "";
}

