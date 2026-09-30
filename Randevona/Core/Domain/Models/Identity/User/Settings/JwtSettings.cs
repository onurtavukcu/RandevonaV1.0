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
    }
}
