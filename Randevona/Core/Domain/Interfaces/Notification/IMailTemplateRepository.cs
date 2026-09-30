using Domain.Entities.Notification;
using Domain.Models.Notification.Email;

namespace Domain.Interfaces.Notification;

public interface IMailTemplateRepository
{
    Task<MailTemplate?> GetActiveTemplateAsync(MailCatalogKey mailCatalogKey);
}
