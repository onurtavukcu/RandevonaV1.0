using Domain.Models.Shared.Settings;

namespace Domain.Models.Identity.User.Settings
{
    public class JwtSettings : ISettings
    {
        public const string SectionName = "JwtSettings";
        public string Key { get; init; } = string.Empty;
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public double DurationInMinutes { get; init; }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key) || System.Text.Encoding.UTF8.GetByteCount(Key) < 32)
                throw new InvalidOperationException("JwtSettings:Key must contain at least 32 UTF-8 bytes.");
            if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
                throw new InvalidOperationException("JwtSettings:Issuer and Audience are required.");
            if (!double.IsFinite(DurationInMinutes) || DurationInMinutes <= 0 || DurationInMinutes > TimeSpan.MaxValue.TotalMinutes)
                throw new InvalidOperationException("JwtSettings:DurationInMinutes must be a valid positive duration.");
        }
    }
}
