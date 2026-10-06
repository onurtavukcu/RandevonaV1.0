using CommonServices.WorkContext.ContextAccessor;
using Data.MongoDbContext;
using Domain.Entities.Whatsapp;
using MongoDB.Driver;

namespace Data.Repositories.Whatsapp;

public sealed class ProviderDataRepository(ITenantDatabaseResolver databases, ITenantContextAccessor accessor,
    IControlMongoDbContext control, IMongoClient client) : IProviderDataRepository
{
    public async Task<ProviderData?> GetAsync(CancellationToken ct = default)
    {
        var scope = accessor.CurrentWorkContext;
        return await (await databases.GetDatabaseAsync(ct)).GetCollection<ProviderData>(nameof(ProviderData))
            .Find(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TrySaveAsync(ProviderData provider, long expectedVersion, ProviderNumberDirectory? newNumber, CancellationToken ct = default)
    {
        var scope = accessor.CurrentWorkContext;
        if (provider.TenantId != scope.TenantId || provider.OrganizationId != scope.OrganizationId ||
            provider.Version != expectedVersion + 1 || (newNumber is not null &&
            (newNumber.TenantId != scope.TenantId || newNumber.OrganizationId != scope.OrganizationId ||
             newNumber.ProviderDataId != provider.Id || !provider.Numbers.Any(x => x.PhoneNumberId == newNumber.PhoneNumberId && x.WabaId == newNumber.WabaId))))
            throw new UnauthorizedAccessException("Connection ownership is inconsistent.");
        var database = await databases.GetDatabaseAsync(ct);
        // Existing tenants also receive the new index before their first connection write.
        await MongoIndexes.EnsureProviderAsync(database, ct);
        var collection = database.GetCollection<ProviderData>(nameof(ProviderData));
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (transaction, token) =>
            {
                if (expectedVersion == 0)
                    await collection.InsertOneAsync(transaction, provider, cancellationToken: token);
                else
                {
                    var update = Builders<ProviderData>.Update.Set(x => x.Numbers, provider.Numbers)
                        .Set(x => x.Version, provider.Version).Set(x => x.Status, provider.Status)
                        .Set(x => x.UpdatedAt, provider.UpdatedAt).Set(x => x.UpdatedByUserId, scope.UserId);
                    var result = await collection.UpdateOneAsync(transaction,
                        x => x.Id == provider.Id && x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
                             x.Version == expectedVersion && x.IsActive && !x.IsDeleted, update, cancellationToken: token);
                    if (result.MatchedCount != 1) throw new ConnectionConflictException();
                }
                if (newNumber is not null)
                    await control.GetCollection<ProviderNumberDirectory>().InsertOneAsync(transaction, newNumber, cancellationToken: token);
                return true;
            }, new TransactionOptions(readConcern: ReadConcern.Snapshot, readPreference: ReadPreference.Primary, writeConcern: WriteConcern.WMajority), ct);
        }
        catch (ConnectionConflictException) { return false; }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }
    private sealed class ConnectionConflictException : Exception;
}

