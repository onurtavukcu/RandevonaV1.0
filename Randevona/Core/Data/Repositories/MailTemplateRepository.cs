using Data.MongoDbContext;
using Domain.Entities.Notification;
using Domain.Interfaces.Notification;
using Domain.Models.Notification.Email;
using MongoDB.Driver;

namespace Data.Repositories;

public class MailTemplateRepository(IControlMongoDbContext context) : IMailTemplateRepository
{
    public async Task<MailTemplate?> GetActiveTemplateAsync(MailCatalogKey mailCatalogKey)
        => await context.Database.GetCollection<MailTemplate>(nameof(MailTemplate))
            .Find(x => x.MailCatalogKey == mailCatalogKey && x.IsActive && !x.IsDeleted)
            .SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
}
