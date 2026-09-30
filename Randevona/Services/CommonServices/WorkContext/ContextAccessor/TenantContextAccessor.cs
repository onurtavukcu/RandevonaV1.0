using CommonServices.WorkContext.ContextHolderMiddleware;
using Domain.Models.Shared.WorkContext;

namespace CommonServices.WorkContext.ContextAccessor
{
    public class TenantContextAccessor : ITenantContextAccessor
    {
        private readonly TenantWorkContextHolder _holder;

        public TenantContextAccessor(TenantWorkContextHolder holder)
        {
            _holder = holder;
        }

        public TenantWorkContext CurrentWorkContext => _holder.Value ?? throw new UnauthorizedAccessException();
    }
}
