using Domain.Entities.Notification;
using Domain.Models.Notification.Email;

namespace Data.Repositories.MailTemplates;

public interface IMailTemplateRepository
{
    Task<MailTemplate?> GetActiveTemplateAsync(MailCatalogKey mailCatalogKey);
}
