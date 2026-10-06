using Domain.Common.Attributes;

namespace Domain.Models.Meta;

public sealed class WhatsAppProviderData
{
    public string WabaId { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string MetaAppId { get; set; } = "";
    [EncryptedField]
    public string SystemUserAccessToken { get; set; } = "";
    public DateTime? AccessTokenExpiresAt { get; set; }
    public string Status { get; set; } = "Configured";
    public DateTime? LastSyncedAt { get; set; }
    public string? VerifiedName { get; set; }
    public bool IsRegistered { get; set; }
}

