using Microsoft.AspNetCore.Authorization;

namespace CommonServices.Authorization;

// Explicit opt-in for operations requiring a validated tenant/branch, including a selected admin workspace.
// Mutating MVC actions must additionally validate antiforgery and the form's expected workspace.
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class TenantOperationAttribute : AuthorizeAttribute;

