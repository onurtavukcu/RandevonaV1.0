using Domain.Entities.BaseEntities;

namespace Domain.Entities.Whatsapp;

public sealed class ProviderNumberDirectory : BaseEntity
{
    public string PhoneNumberId { get; set; } = "";
    public string WabaId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public string ProviderDataId { get; set; } = "";
}

