using Data.Repositories.BaseRepositories;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Password;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
//using Domain.Models.Notification.Email;
using Domain.Models.Shared.Result;
//using Infrastructure.NotificationServices.Email.CustomMailGenerators;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityService.PasswordService;

public class PasswordService : IPasswordService
{
    private const int ResetLifetimeMinutes = 5;
    private const int MaxVerificationAttempts = 5;
    private readonly JwtSettings _jwtSettings;
    private readonly IRepository<Users> _userRepository;
    //private readonly IMailDefinitionClient _mailDefinitionClient;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(JwtSettings jwtSettings, IRepository<Users> userRepository, ILogger<PasswordService> logger)
        => (_jwtSettings, _userRepository, _logger) = (jwtSettings, userRepository, logger);

    public string HashPassword(string password)
    {
        if (!IsValidPassword(password))
            throw new ArgumentException("Password must be non-empty and at most 72 UTF-8 bytes.", nameof(password));
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (!IsValidPassword(password) || string.IsNullOrWhiteSpace(hashedPassword)) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, hashedPassword); }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }

    public async Task<Result<string>> ForgotPassword(Users user)
    {
        ArgumentNullException.ThrowIfNull(user);
        // Use the persisted account, never an email address supplied in a detached user object.
        var account = await _userRepository.GetByIdAsync(user.Id);
        if (account is null || !account.IsActive || account.UserStatus != UserStatus.Active ||
            string.IsNullOrWhiteSpace(account.Email)) return InvalidReset();

        var expiresAt = DateTime.UtcNow.AddMinutes(ResetLifetimeMinutes);
        var state = new ForgotPassword
        {
            VerificationCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(),
            ResetToken = GeneratePasswordResetToken(account.Id, expiresAt),
            ExpiresAt = expiresAt
        };
        var changed = await _userRepository.UpdateManyAsync(
            x => x.Id == account.Id && x.IsActive && x.UserStatus == UserStatus.Active,
            Builders<Users>.Update.Set(x => x.ForgotPassword, state));
        if (changed != 1) return InvalidReset();

        // TODO: Restore delivery when the email service is ready.
        // The code remains in the reset record; never return it to an anonymous caller or log it.
        /*
        try
        {
            await _mailDefinitionClient.SendByMailDefinitionAsync(new SendMailByDefinitionRequest
            {
                Locale = "tr-TR", SourceService = "IdentityService", To = [account.Email],
                Variables = new() { ["verificationCode"] = state.VerificationCode },
                TenantId = account.TenantId, MailCatalogKey = MailCatalogKey.Identity_ForgotPassword
            });
            return state.ResetToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset email could not be sent.");
            // Do not invalidate a newer request if another reset started while sending mail.
            await _userRepository.UpdateManyAsync(
                x => x.Id == account.Id && x.ForgotPassword != null && x.ForgotPassword.ResetToken == state.ResetToken,
                Builders<Users>.Update.Set(x => x.ForgotPassword, null));
            return new Error("Password.EmailFailed", "Şifre sıfırlama e-postası gönderilemedi.", ErrorType.Failure);
        }
        */
        _logger.LogInformation("Password reset request prepared; email delivery is currently disabled.");
        return state.ResetToken;
    }
    public async Task<Result<bool>> ConfirmVerificationCode(VerificationCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.ResetToken) ||
            string.IsNullOrWhiteSpace(request.VerificationCode)) return InvalidReset();
        var user = await FindActiveUserAsync(request.Email);
        var state = user?.ForgotPassword;
        if (user is null || state is null || state.IsUsed || state.IsVerified ||
            state.ResetToken != request.ResetToken || state.ExpiresAt <= DateTime.UtcNow ||
            state.VerificationAttempts >= MaxVerificationAttempts) return InvalidReset();

        var validCode = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state.VerificationCode), Encoding.UTF8.GetBytes(request.VerificationCode));
        var now = DateTime.UtcNow;
        var update = Builders<Users>.Update.Set(x => x.ForgotPassword!.VerificationAttempts, state.VerificationAttempts + 1);
        if (validCode)
            update = update.Set(x => x.ForgotPassword!.IsVerified, true)
                .Set(x => x.ForgotPassword!.VerificationCode, string.Empty);

        // Compare-and-set prevents concurrent confirmations from losing the attempt count.
        var changed = await _userRepository.UpdateManyAsync(x => x.Id == user.Id && x.IsActive &&
            x.UserStatus == UserStatus.Active && x.ForgotPassword != null &&
            x.ForgotPassword.ResetToken == request.ResetToken && !x.ForgotPassword.IsUsed &&
            !x.ForgotPassword.IsVerified && x.ForgotPassword.ExpiresAt > now &&
            x.ForgotPassword.VerificationAttempts == state.VerificationAttempts, update);
        if (validCode && changed == 1) return true;
        return InvalidReset();
    }

    public async Task<Result<bool>> ResetPassword(ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsValidPassword(request.NewPassword) || request.NewPassword != request.ConfirmPassword)
            return new Error("Password.InvalidPassword", "Şifreler aynı, boş olmayan ve en fazla 72 UTF-8 bayt uzunluğunda olmalı.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.ResetToken)) return InvalidReset();
        var user = await FindActiveUserAsync(request.Email);
        var state = user?.ForgotPassword;
        if (user is null || state is null || !state.IsVerified || state.IsUsed ||
            state.ResetToken != request.ResetToken || state.ExpiresAt <= DateTime.UtcNow) return InvalidReset();

        var hash = HashPassword(request.NewPassword);
        var now = DateTime.UtcNow;
        // Password change and token consumption are one conditional MongoDB document update.
        var changed = await _userRepository.UpdateManyAsync(x => x.Id == user.Id && x.IsActive &&
            x.UserStatus == UserStatus.Active && x.ForgotPassword != null &&
            x.ForgotPassword.IsVerified && !x.ForgotPassword.IsUsed &&
            x.ForgotPassword.ResetToken == request.ResetToken && x.ForgotPassword.ExpiresAt > now,
            Builders<Users>.Update.Set(x => x.PasswordHash, hash)
                .Set(x => x.ForgotPassword!.IsUsed, true)
                .Set(x => x.ForgotPassword!.PasswordChangedAt, now)
                .Set(x => x.ForgotPassword!.VerificationCode, string.Empty)
                .Set(x => x.ForgotPassword!.ResetToken, string.Empty));
        if (changed == 1) return true;
        return InvalidReset();
    }

    private Task<Users?> FindActiveUserAsync(string email)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        return _userRepository.FindOneAsync(x => x.NormalizedEmail == normalizedEmail &&
            x.IsActive && x.UserStatus == UserStatus.Active);
    }

    private static bool IsValidPassword(string? password)
        => PasswordPolicy.IsValid(password);

    private static Error InvalidReset() => new("Password.InvalidReset",
        "Şifre sıfırlama isteği geçersiz, süresi dolmuş veya doğrulanmamış.", ErrorType.Validation);

    private string GeneratePasswordResetToken(string userId, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key) || Encoding.UTF8.GetByteCount(_jwtSettings.Key) < 32)
            throw new InvalidOperationException("JWT key must contain at least 32 UTF-8 bytes.");
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim("type", "password_reset"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())]),
            Expires = expiresAt, Issuer = _jwtSettings.Issuer, Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        return tokenHandler.WriteToken(token);
    }
}

