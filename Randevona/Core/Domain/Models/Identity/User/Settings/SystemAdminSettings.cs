using Domain.Models.Identity.User.Password;
using Domain.Models.Shared.Settings;
using System.ComponentModel.DataAnnotations;

namespace Domain.Models.Identity.User.Settings;

public class SystemAdminSettings : ISettings
{
    public string[] Emails { get; set; } = [];
    public string DefaultPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = "System";
    public string LastName { get; set; } = "Administrator";

    public void ValidateEmails()
    {
        if (Emails is null || Emails.Any(email => string.IsNullOrWhiteSpace(email) ||
            email.Trim().Length > 254 || !new EmailAddressAttribute().IsValid(email.Trim())))
            throw new InvalidOperationException("SystemAdminSettings:Emails must contain valid email addresses.");
    }

    public void ValidateNewAccount()
    {
        if (!PasswordPolicy.IsValid(DefaultPassword))
            throw new InvalidOperationException("SystemAdminSettings:DefaultPassword must not be empty and must be at most 72 UTF-8 bytes.");
        if (string.IsNullOrWhiteSpace(FirstName) || FirstName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(LastName) || LastName.Trim().Length > 100)
            throw new InvalidOperationException("SystemAdminSettings:FirstName and LastName are required and must not exceed 100 characters.");
    }
}
