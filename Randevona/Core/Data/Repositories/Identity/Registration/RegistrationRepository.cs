using Data.MongoDbContext;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.UserInformation;
using MongoDB.Driver;

namespace Data.Repositories.Identity.Registration;

public class RegistrationRepository(IControlMongoDbContext control, IMongoClient client) : IRegistrationRepository
{
    // Include deleted accounts: the unique email index also reserves their addresses.
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => control.GetCollection<Users>().Find(x => x.NormalizedEmail == normalizedEmail).AnyAsync(ct);

    public Task<bool> DatabaseNameExistsAsync(string databaseName, CancellationToken ct = default)
        => control.GetCollection<Tenants>().Find(x => x.DatabaseName == databaseName).AnyAsync(ct);

    public Task<Users?> FindUserByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => control.GetCollection<Users>().Find(x => x.NormalizedEmail == normalizedEmail).FirstOrDefaultAsync(ct)!;

    public async Task ClearSystemAdminTenantAsync(string userId, CancellationToken ct = default)
    {
        // Migrate only configured, active platform admins. Keep tenant records and databases intact.
        await control.GetCollection<Users>().UpdateOneAsync(x => x.Id == userId &&
            x.SystemRole == SystemUserRoleType.SuperAdmin && x.IsActive && !x.IsDeleted && x.UserStatus == UserStatus.Active,
            Builders<Users>.Update.Set(x => x.TenantId, null).Set(x => x.HasAllOrganizationAccess, false)
                .Set(x => x.Memberships, new List<UserOrganizationMembership>()).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
    }

    public async Task<bool> TryCreateUserAsync(Users user, CancellationToken ct = default)
    {
        try
        {
            await control.GetCollection<Users>().InsertOneAsync(user, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

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
