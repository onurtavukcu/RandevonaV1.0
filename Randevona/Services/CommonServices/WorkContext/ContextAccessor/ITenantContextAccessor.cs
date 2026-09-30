using Domain.Models.Shared.WorkContext;

namespace CommonServices.WorkContext.ContextAccessor
{
    public interface ITenantContextAccessor
    {
        TenantWorkContext CurrentWorkContext { get; }
    }
}
