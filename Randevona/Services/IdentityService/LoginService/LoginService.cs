using CommonServices.WorkContext.ContextAccessor;
using Data.Repositories.Identity.Login;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Login;
using Domain.Models.Identity.User.Password;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.Result;
using IdentityService.PasswordService;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IdentityService.LoginService;

public class LoginService(ILoginRepository loginRepository, IPasswordService passwordService,
    JwtSettings jwtSettings, ITenantWorkContextResolver contextResolver, ILogger<LoginService> logger) : ILoginService
{
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        var normalizedEmail = request.Email?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !PasswordPolicy.IsValid(request.Password))
            return InvalidCredentials();

        try
        {
            var user = await loginRepository.GetUserByEmailAsync(normalizedEmail, ct);
            if (user is null || user.IsDeleted || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
                return InvalidCredentials();
            if (!user.IsActive)
                return new Error("Login.InactiveAccount", "Your Account Is Inactive.", ErrorType.Validation);
            if (user.UserStatus == UserStatus.PendingApproval)
                return new Error("Login.PendingApproval", "Your Application Is Pending Approval.", ErrorType.Validation);
            if (user.UserStatus != UserStatus.Active || !Enum.IsDefined(user.SystemRole))
                return new Error("Login.InactiveAccount", "Your Account Is Inactive.", ErrorType.Validation);
            string? tenantId = null;
            string? organizationId = null;
            var role = user.SystemRole;
            if (role == SystemUserRoleType.SuperAdmin)
            {
                // Platform access is verified in the central Users collection only.
                await contextResolver.ValidatePlatformAdminAsync(user.Id, ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(user.TenantId))
                    return new Error("Login.TenantUnavailable", "Your Account Is Not Associated With A Tenant.", ErrorType.Validation);
                var context = await contextResolver.ResolveAsync(user.Id, user.TenantId, null, ct);
                if (string.IsNullOrWhiteSpace(context.OrganizationId))
                    return new Error("Login.OrganizationUnavailable", "No Active Branch Available For Your Account.", ErrorType.Validation);
                tenantId = context.TenantId;
                organizationId = context.OrganizationId;
                role = context.SystemUserRoleType;
            }
            var issuedAt = DateTimeOffset.UtcNow;
            jwtSettings.Validate();
            var expiresAt = issuedAt.AddMinutes(jwtSettings.DurationInMinutes);
            return new LoginResponse
            {
                Success = true, Token = GenerateJwtToken(user, tenantId, organizationId, role, issuedAt, expiresAt),
                UserId = user.Id, Email = user.Email, TenantId = tenantId,
                OrganizationId = organizationId, Role = role.ToString(),
                IssuedAtUtc = issuedAt, ExpiresAtUtc = expiresAt
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException)
        {
            return new Error("Login.TenantUnavailable", "Your Account Or Tenant Access Is Not Available.", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            logger.LogError("Login failed ({ErrorType}).", ex.GetType().Name);
            return new Error("Login.Failed", "Login failed. Please try again.", ErrorType.Failure);
        }
    }

    private static Error InvalidCredentials() => new("Login.InvalidCredentials", "Invalid email or password.", ErrorType.Validation);

    private string GenerateJwtToken(Users user, string? tenantId, string? organizationId, SystemUserRoleType role, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var handler = new JwtSecurityTokenHandler();
        var identity = new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("type", "access"),
            new Claim(ClaimTypes.Role, role.ToString())]);
        if (tenantId is not null) identity.AddClaim(new Claim("tenantId", tenantId));
        if (organizationId is not null) identity.AddClaim(new Claim("organizationId", organizationId));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            IssuedAt = issuedAt.UtcDateTime, NotBefore = issuedAt.UtcDateTime, Expires = expiresAt.UtcDateTime,
            Issuer = jwtSettings.Issuer, Audience = jwtSettings.Audience, TokenType = "at+jwt",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                SecurityAlgorithms.HmacSha256)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
