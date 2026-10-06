using Domain.Entities.BaseEntities;
using Domain.Models.Meta;

namespace Domain.Entities.Whatsapp;

public enum ProviderType { Whatsapp }
public enum ConnectionStatus { Configured, AccessVerified, Disabled }

public sealed class ProviderData : OrganizationBaseEntity
{
    public string ConnectedByUserId { get; set; } = "";
    public string? UpdatedByUserId { get; set; }
    public string DisplayName { get; set; } = "";
    public ProviderType Provider { get; set; } = ProviderType.Whatsapp;
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Configured;
    public long Version { get; set; } = 1;
    public List<WhatsAppProviderData> Numbers { get; set; } = new();
}
