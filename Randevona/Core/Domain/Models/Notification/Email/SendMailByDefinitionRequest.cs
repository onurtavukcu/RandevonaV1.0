namespace Domain.Models.Notification.Email
{
    public class SendMailByDefinitionRequest
    {
        public MailCatalogKey MailCatalogKey { get; set; } = MailCatalogKey.Unknown;
        public List<string> To { get; set; } = new();
        public Dictionary<string, string> Variables { get; set; } = new();
        public string SourceService { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string? Locale { get; set; }
    }
}
