using Data.Repositories.Identity.Login;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Login;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.Result;
using IdentityService.PasswordService;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;

namespace IdentityService.LoginService
{
    public class LoginService(ILoginRepository loginRepository, IPasswordService passwordService, JwtSettings jwtSettings, ILogger<LoginService> logger) : ILoginService
    {
        public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var normalizedEmail = request.Email?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new InvalidCredentialException();

            var user = await loginRepository.GetUserByEmailAsync(normalizedEmail);

            if (user is null)
                return new Error("Register.InvalidDetails", "User Not Found.", ErrorType.Validation);

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !user.PasswordHash.StartsWith("$2"))
                return new Error("Register.InvalidDetails", "User Not Found.", ErrorType.Validation);

            if (!passwordService.VerifyPassword(request.Password, user.PasswordHash))
                return new Error("Register.InvalidDetails", "Invalid password.", ErrorType.Validation);

            if (user.UserStatus == UserStatus.PendingApproval)
                return new Error("Register.InvalidDetails", "User is pending approval.", ErrorType.Validation);

            if (user.UserStatus == UserStatus.Rejected || user.UserStatus == UserStatus.Inactive || user.UserStatus == UserStatus.Suspended)
                return new Error("Register.InvalidDetails", "Inactive user.", ErrorType.Validation);


            //TODO : FEED Main Screen with org and tenant and user details. Also, check if user has access to the org and tenant.

            return new LoginResponse
            {
                Success = true,
                Token = GenerateJwtToken(user),
                UserId = user.Id ?? string.Empty,
                Email = user.Email ?? string.Empty,
                TenantId = user.TenantId ?? string.Empty,
                Role = user.SystemRole.ToString()
            };
        }

        private string GenerateJwtToken(Users user)  // Todo düzenlenecek
        {
            if (string.IsNullOrWhiteSpace(jwtSettings.Key))
                throw new InvalidOperationException("JWT configuration is missing");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

            var tenantId = user.TenantId ?? string.Empty;

            var userMembership = user.Memberships?.Select(m => m.OrganizationId).ToList() ?? new List<string>();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("tenantId", tenantId),
                new Claim("userid", user.Id ?? string.Empty),
                //new Claim("userRoleType", user.UserRoleType.ToString()), // Todo rolltype
                new Claim(ClaimTypes.Role, user.SystemRole.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(jwtSettings.DurationInMinutes),
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
