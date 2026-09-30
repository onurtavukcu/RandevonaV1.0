namespace Domain.Models.Notification.Email
{
    public class MailDefinitionResult
    {
        public bool Success { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
    }
}
