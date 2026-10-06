using Microsoft.AspNetCore.Authorization;

namespace CommonServices.Authorization;

// Shared GET pages. Admin tenant context requires an explicitly selected, revalidated workspace.
// This marker never authorizes tenant mutations; those need a separate operation policy.
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class WorkspacePageAttribute : AuthorizeAttribute;
