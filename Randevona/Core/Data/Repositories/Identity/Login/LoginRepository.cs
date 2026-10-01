using Data.MongoDbContext;
using Domain.Entities.Identity.UserEntity;
using MongoDB.Driver;

namespace Data.Repositories.Identity.Login
{
    public class LoginRepository(IControlMongoDbContext control) : ILoginRepository
    {
        public async Task<Users?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct = default)
       => await control.GetCollection<Users>().Find(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted).FirstOrDefaultAsync(ct);
    }
}
