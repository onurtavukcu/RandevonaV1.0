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
            if (string.IsNullOrWhiteSpace(user.TenantId))
                return new Error("Login.TenantUnavailable", "Your Account Is Not Associated With A Tenant.", ErrorType.Validation);

            // Validate persisted tenant mapping, account status and accessible branches before issuing a session.
            var context = await contextResolver.ResolveAsync(user.Id, user.TenantId, null, ct);
            if (string.IsNullOrWhiteSpace(context.OrganizationId))
                return new Error("Login.OrganizationUnavailable", "No Active Branch Available For Your Account.", ErrorType.Validation);
            var issuedAt = DateTimeOffset.UtcNow;
            jwtSettings.Validate();
            var expiresAt = issuedAt.AddMinutes(jwtSettings.DurationInMinutes);
            return new LoginResponse
            {
                Success = true, Token = GenerateJwtToken(user, context.OrganizationId, context.SystemUserRoleType, issuedAt, expiresAt),
                UserId = user.Id, Email = user.Email, TenantId = context.TenantId,
                OrganizationId = context.OrganizationId, Role = context.SystemUserRoleType.ToString(),
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

    private string GenerateJwtToken(Users user, string organizationId, SystemUserRoleType role, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("tenantId", user.TenantId!),
                new Claim("organizationId", organizationId),
                new Claim("type", "access"),
                new Claim(ClaimTypes.Role, role.ToString())]),
            IssuedAt = issuedAt.UtcDateTime, NotBefore = issuedAt.UtcDateTime, Expires = expiresAt.UtcDateTime,
            Issuer = jwtSettings.Issuer, Audience = jwtSettings.Audience, TokenType = "at+jwt",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                SecurityAlgorithms.HmacSha256)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
