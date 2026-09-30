using Domain.Models.Identity.User.UserInformation;

namespace Domain.Models.Identity.User.Register;

public class RegisterResponse
{
    public UserStatus UserStatus { get; init; } = UserStatus.PendingApproval;
    public bool IsTenantReady { get; init; }
}
