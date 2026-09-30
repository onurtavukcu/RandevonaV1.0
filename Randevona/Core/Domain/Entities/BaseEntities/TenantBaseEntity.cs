namespace Domain.Entities.BaseEntities
{
    public class TenantBaseEntity : BaseEntity
    {
        public string TenantId { get; set; } = string.Empty;
    }
}
