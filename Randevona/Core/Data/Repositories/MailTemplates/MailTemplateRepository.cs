using Data.MongoDbContext;
using Domain.Models.Notification.Email;
using Domain.Entities.Notification;
using MongoDB.Driver;

namespace Data.Repositories.MailTemplates;

public class MailTemplateRepository(IControlMongoDbContext context) : IMailTemplateRepository
{
    public async Task<MailTemplate?> GetActiveTemplateAsync(MailCatalogKey mailCatalogKey)
        => await context.Database.GetCollection<MailTemplate>(nameof(MailTemplate))
            .Find(x => x.MailCatalogKey == mailCatalogKey && x.IsActive && !x.IsDeleted)
            .SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
}
