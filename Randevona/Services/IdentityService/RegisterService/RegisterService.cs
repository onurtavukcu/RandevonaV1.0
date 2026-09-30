using Data.MongoDbContext;
using Data.Repositories.Identity.Registration;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Password;
using Domain.Models.Identity.User.Register;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.Result;
using IdentityService.PasswordService;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace IdentityService.RegisterService;

public class RegisterService(IRegistrationRepository registrations, IPasswordService passwords,
    TenantProvisioningService provisioning, ILogger<RegisterService> logger) : IRegisterService
{
    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var companyName = request.CompanyName?.Trim() ?? string.Empty;
        var organizationName = request.OrganizationName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        if (firstName.Length is < 1 or > 100 || lastName.Length is < 1 or > 100 ||
            companyName.Length is < 1 or > 200 || organizationName.Length is < 1 or > 200)
            return new Error("Register.InvalidDetails", "Ad, soyad, işletme ve şube bilgilerini kontrol edin.", ErrorType.Validation);
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            return new Error("Register.InvalidEmail", "Geçerli bir e-posta adresi girin.", ErrorType.Validation);
        if (!PasswordPolicy.IsValid(request.Password))
            return new Error("Register.InvalidPassword", "Şifre boş olamaz ve en fazla 72 UTF-8 bayt olabilir.", ErrorType.Validation);
        if (request.Password != request.ConfirmPassword)
            return new Error("Register.PasswordMismatch", "Şifreler eşleşmiyor.", ErrorType.Validation);

        var normalizedEmail = email.ToUpperInvariant();
        var user = new Users
        {
            FirstName = firstName, LastName = lastName, Email = email, NormalizedEmail = normalizedEmail,
            SystemRole = SystemUserRoleType.User, UserStatus = UserStatus.PendingApproval,
            HasAllOrganizationAccess = true
        };
        var tenant = new Tenants
        {
            OwnerUserId = user.Id,
            CompanyInfos = new CompanyInfos { CompanyName = companyName, ContactEmail = email }
        };
        TenantProvisioningService.PrepareNewTenant(tenant, organizationName);
        user.TenantId = tenant.Id;
        user.Memberships.Add(new UserOrganizationMembership { OrganizationId = tenant.DefaultOrganizationId });

        try
        {
            if (await registrations.EmailExistsAsync(normalizedEmail, ct)) return DuplicateEmail();
            user.PasswordHash = passwords.HashPassword(request.Password);
            // The unique index, not this preliminary lookup, arbitrates concurrent registrations.
            if (!await registrations.TryCreateAsync(user, tenant, ct)) return DuplicateEmail();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Do not expose database errors, submitted fields or credentials in the result/log.
            logger.LogError("Registration persistence failed ({ErrorType}).", ex.GetType().Name);
            return new Error("Register.PersistenceFailed", "Kayıt işlemi tamamlanamadı. Lütfen tekrar deneyin.", ErrorType.Failure);
        }

        try
        {
            await provisioning.ProvisionAsync(tenant.Id, ct);
            return new RegisterResponse { IsTenantReady = true };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // The central application already exists. Preserve it for the superadmin retry/approval flow.
            logger.LogWarning("Tenant {TenantId} awaits provisioning retry ({ErrorType}).", tenant.Id, ex.GetType().Name);
            return new RegisterResponse { IsTenantReady = false };
        }
    }

    private static Error DuplicateEmail() => new("Register.EmailExists",
        "Bu e-posta adresiyle daha önce bir kayıt yapılmış. Mevcut başvurunuz varsa onay bekliyor olabilir.", ErrorType.Conflict);
}
