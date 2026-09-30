using Domain.Models.Shared.Settings;

namespace Domain.Models.Notification.Email
{
    public class SmtpSettings : ISettings
    {
        public const string SectionName = "SmtpSettings";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string FromAddress { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
