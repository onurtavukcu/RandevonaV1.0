namespace Domain.Models.Identity.User.Password
{
    public class VerificationCodeRequest
    {
        public string Email { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
    }
}
