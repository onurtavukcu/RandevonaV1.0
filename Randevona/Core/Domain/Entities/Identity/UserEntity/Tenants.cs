using Domain.Entities.BaseEntities;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Identity.Tenant;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities.Identity.UserEntity
{
    public class Tenants : BaseEntity
    {
        public string DatabaseName { get; set; } = string.Empty;
        public TenantProvisioningStatus ProvisioningStatus { get; set; } = TenantProvisioningStatus.Pending;
        public string InitialOrganizationName { get; set; } = string.Empty;
        public string? ProvisioningLeaseId { get; set; }
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ProvisioningLeaseUntilUtc { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public CompanyInfos CompanyInfos { get; set; } = new();
        public string DefaultOrganizationId { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = "Europe/Istanbul";
    }
}
