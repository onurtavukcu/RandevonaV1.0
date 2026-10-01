using Data.Repositories.Identity.Registration;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Settings;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.PasswordService;
using Microsoft.Extensions.Logging;

namespace IdentityService.Seeders;

public class SystemAdminSeeder(IRegistrationRepository registrations, IPasswordService passwords, ILogger<SystemAdminSeeder> logger)
{
    public async Task SeedAsync(SystemAdminSettings settings, CancellationToken ct = default)
    {
        settings.ValidateEmails();
        foreach (var email in settings.Emails.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var normalizedEmail = email.ToUpperInvariant();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var existing = await registrations.FindUserByEmailAsync(normalizedEmail, ct);
                if (existing is not null)
                {
                    if (existing.SystemRole != SystemUserRoleType.SuperAdmin || !existing.IsActive || existing.IsDeleted ||
                        existing.UserStatus != UserStatus.Active)
                        logger.LogWarning("Configured account is not an active SuperAdmin; no changes were made.");
                    else if (existing.TenantId is not null || existing.HasAllOrganizationAccess || existing.Memberships.Count > 0)
                        await registrations.ClearSystemAdminTenantAsync(existing.Id, ct);
                    break;
                }
                settings.ValidateNewAccount();
                var user = new Users
                {
                    FirstName = settings.FirstName.Trim(), LastName = settings.LastName.Trim(),
                    Email = email, NormalizedEmail = normalizedEmail,
                    PasswordHash = passwords.HashPassword(settings.DefaultPassword),
                    SystemRole = SystemUserRoleType.SuperAdmin, UserStatus = UserStatus.Active,
                    IsActive = true, IsDeleted = false, TenantId = null,
                    HasAllOrganizationAccess = false, Memberships = []
                };
                if (await registrations.TryCreateUserAsync(user, ct))
                {
                    logger.LogInformation("Platform administrator account is ready.");
                    break;
                }
                // Another server may have created the same email. Re-read; never overwrite it.
                if (attempt == 2) throw new InvalidOperationException("System administrator setup could not resolve an account conflict.");
            }
        }
    }
}
