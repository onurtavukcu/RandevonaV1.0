using CommonServices.WorkContext.ContextAccessor;
using Data.MongoDbContext;
using Domain.Entities.BaseEntities;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Shared.WorkContext;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Data.Repositories.BaseRepositories;

public class ScopedRepository<T> : Repository<T>, IScopedRepository<T> where T : TenantBaseEntity
{
    private readonly ITenantContextAccessor _accessor;
    public ScopedRepository(IMongoDbContext context, ITenantContextAccessor accessor)
        : base(ct => context.GetCollectionAsync<T>(ct)) => _accessor = accessor;

    private TenantWorkContext Context
    {
        get
        {
            var context = _accessor.CurrentWorkContext;
            if (!ObjectId.TryParse(context.TenantId, out _)) throw new UnauthorizedAccessException("Tenant context is missing.");
            if (!string.IsNullOrEmpty(context.OrganizationId) &&
                (!ObjectId.TryParse(context.OrganizationId, out _) ||
                 (!context.HasAllOrganizationAccess && !(context.AllowedOrganizationIds?.Contains(context.OrganizationId) ?? false))))
                throw new UnauthorizedAccessException("Organization access denied.");
            return context;
        }
    }

    protected override FilterDefinition<T> Scope
    {
        get
        {
            var context = Context;
            var filter = base.Scope & Builders<T>.Filter.Eq(x => x.TenantId, context.TenantId);
            var branchField = typeof(T) == typeof(Organizations) ? "_id"
                : typeof(OrganizationBaseEntity).IsAssignableFrom(typeof(T)) ? "OrganizationId" : null;
            if (branchField is null) return filter;
            if (!string.IsNullOrEmpty(context.OrganizationId))
                return filter & BranchFilter(branchField, [context.OrganizationId]);
            return context.HasAllOrganizationAccess ? filter
                : filter & BranchFilter(branchField, context.AllowedOrganizationIds ?? []);
        }
    }

    private static FilterDefinition<T> BranchFilter(string field, IEnumerable<string> ids)
    {
        var values = ids.Where(id => ObjectId.TryParse(id, out _)).Select(id =>
            field == "_id" ? (BsonValue)ObjectId.Parse(id) : new BsonString(id));
        return new BsonDocumentFilterDefinition<T>(new BsonDocument(field, new BsonDocument("$in", new BsonArray(values))));
    }

    private void ApplyOwnership(T entity, bool creating)
    {
        var context = Context;
        if (!string.IsNullOrEmpty(entity.TenantId) && entity.TenantId != context.TenantId)
            throw new UnauthorizedAccessException("Tenant ownership cannot change.");
        entity.TenantId = context.TenantId;
        if (entity is OrganizationBaseEntity branchEntity)
        {
            if (creating && string.IsNullOrEmpty(branchEntity.OrganizationId)) branchEntity.OrganizationId = context.OrganizationId;
            ValidateBranch(branchEntity.OrganizationId, context);
        }
        else if (entity is Organizations) ValidateBranch(entity.Id, context);
    }

    private static void ValidateBranch(string id, TenantWorkContext context)
    {
        if (!ObjectId.TryParse(id, out _) ||
            (!string.IsNullOrEmpty(context.OrganizationId) && context.OrganizationId != id) ||
            (!context.HasAllOrganizationAccess && !(context.AllowedOrganizationIds?.Contains(id) ?? false)))
            throw new UnauthorizedAccessException("Organization access denied.");
    }

    protected override void PrepareCreate(T entity)
    {
        ApplyOwnership(entity, true);
        base.PrepareCreate(entity);
    }
    protected override FilterDefinition<T> EntityFilter(T entity)
    {
        ApplyOwnership(entity, false);
        var filter = base.EntityFilter(entity);
        if (entity is OrganizationBaseEntity branch)
            filter &= Builders<T>.Filter.Eq("OrganizationId", branch.OrganizationId);
        return filter;
    }
}

