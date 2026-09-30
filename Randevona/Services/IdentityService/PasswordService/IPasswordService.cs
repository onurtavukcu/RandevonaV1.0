using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Password;
using Domain.Models.Shared.Result;

namespace IdentityService.PasswordService
{
    public interface IPasswordService
    {
        Task<Result<string>> ForgotPassword(Users user);
        Task<Result<bool>> ConfirmVerificationCode(VerificationCodeRequest verificationCodeRequest);
        Task<Result<bool>> ResetPassword(ResetPasswordRequest resetPasswordRequest);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }
}
