namespace Domain.Entities.BaseEntities
{
    public class OrganizationBaseEntity : TenantBaseEntity
    {
        public string OrganizationId { get; set; } = string.Empty;
    }
}
