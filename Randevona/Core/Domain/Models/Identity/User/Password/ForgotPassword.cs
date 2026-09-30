using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Models.Identity.User.Password
{
    [BsonIgnoreExtraElements]
    public class ForgotPassword
    {
        public DateTime PasswordChangedAt { get; set; }
        public string VerificationCode { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; } = false;
        public bool IsVerified { get; set; }
        public int VerificationAttempts { get; set; }
    }
}
