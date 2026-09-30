using Domain.Entities.BaseEntities;
using Domain.Models.Notification.Email;

namespace Domain.Entities.Notification;

// Platform-owned mail definitions are shared in the control database.
public class MailTemplate : BaseEntity
{
    public MailCatalogKey MailCatalogKey { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyHtmlTemplate { get; set; } = string.Empty;
}
