using Domain.Common.Attributes;
using Domain.Entities.BaseEntities;
using Domain.Models.Identity.User.Password;
using Domain.Models.Identity.User.UserInformation;

namespace Domain.Entities.Identity.UserEntity
{
    public class Users : BaseEntity
    {
        public string? TenantId { get; set; }
        [EncryptedField]
        public string FirstName { get; set; } = string.Empty;
        [EncryptedField]
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public ForgotPassword? ForgotPassword { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public SystemUserRoleType SystemRole { get; set; } = SystemUserRoleType.User;
        public UserStatus UserStatus { get; set; } = UserStatus.Active;
        public bool HasAllOrganizationAccess { get; set; } 
        public List<UserOrganizationMembership> Memberships { get; set; } = new();
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
    }
}

