using Domain.Entities.BaseEntities;
using Domain.Models.Identity.User.UserInformation;

namespace Domain.Entities.Identity.UserEntity
{
    public class Organizations : TenantBaseEntity
    {
        public OrganizationInfos OrganizationInfos { get; set; } = new();
    }
}
