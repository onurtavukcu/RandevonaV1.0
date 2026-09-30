using Domain.Entities.BaseEntities;
namespace Data.Repositories;
public interface IScopedRepository<T> : IRepository<T> where T : TenantBaseEntity { }
