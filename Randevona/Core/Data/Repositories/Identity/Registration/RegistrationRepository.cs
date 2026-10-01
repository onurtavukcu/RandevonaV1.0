using Data.MongoDbContext;
using Domain.Entities.Identity.UserEntity;
using MongoDB.Driver;

namespace Data.Repositories.Identity.Registration;

public class RegistrationRepository(IControlMongoDbContext control, IMongoClient client) : IRegistrationRepository
{
    // Include deleted accounts: the unique email index also reserves their addresses.
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => control.GetCollection<Users>().Find(x => x.NormalizedEmail == normalizedEmail).AnyAsync(ct);

    public Task<bool> DatabaseNameExistsAsync(string databaseName, CancellationToken ct = default)
        => control.GetCollection<Tenants>().Find(x => x.DatabaseName == databaseName).AnyAsync(ct);

    public async Task<bool> TryCreateAsync(Users user, Tenants tenant, CancellationToken ct = default)
    {
        if (user.TenantId != tenant.Id || tenant.OwnerUserId != user.Id)
            throw new InvalidOperationException("Registration ownership is inconsistent.");
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (transaction, token) =>
            {
                // Both inserts must use this session; a tenant insert failure rolls back the user too.
                await control.GetCollection<Users>().InsertOneAsync(transaction, user, cancellationToken: token);
                await control.GetCollection<Tenants>().InsertOneAsync(transaction, tenant, cancellationToken: token);
                return true;
            }, new TransactionOptions(readConcern: ReadConcern.Snapshot,
                readPreference: ReadPreference.Primary, writeConcern: WriteConcern.WMajority), ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
