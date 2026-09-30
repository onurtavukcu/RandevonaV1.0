using Domain.Models.Shared.WorkContext;
namespace CommonServices.WorkContext.ContextHolderMiddleware;
public class TenantWorkContextHolder
{
    public TenantWorkContext? Value { get; private set; }
    public void Set(TenantWorkContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (Value is not null) throw new InvalidOperationException("WorkContext cannot change inside an existing scope.");
        Value = ctx with { AllowedOrganizationIds = Array.AsReadOnly((ctx.AllowedOrganizationIds ?? []).Distinct().ToArray()) };
    }
}
