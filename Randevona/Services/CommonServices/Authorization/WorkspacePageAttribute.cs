using Microsoft.AspNetCore.Authorization;

namespace CommonServices.Authorization;

// Only for shared, read-only shell pages. This does not grant tenant data access.
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class WorkspacePageAttribute : AuthorizeAttribute;
