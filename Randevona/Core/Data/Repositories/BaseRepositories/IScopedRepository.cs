using Domain.Entities.BaseEntities;
namespace Data.Repositories.BaseRepositories;
public interface IScopedRepository<T> : IRepository<T> where T : TenantBaseEntity { }
