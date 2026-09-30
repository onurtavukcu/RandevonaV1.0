namespace Domain.Models.Notification.Email
{
    public class EmailMessage
    {
        public List<string> To { get; set; } = new();
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public MailCatalogKey MailCatalogKey { get; set; } = MailCatalogKey.Unknown;
        public Dictionary<string, string> Variables { get; set; } = new();
    }
}
