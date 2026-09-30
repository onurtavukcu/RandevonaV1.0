using System.Text;

namespace Domain.Models.Identity.User.Password;

public static class PasswordPolicy
{
    // BCrypt limit is measured in UTF-8 bytes, not characters. No minimum length of 12.
    public static bool IsValid(string? password)
        => !string.IsNullOrWhiteSpace(password) && Encoding.UTF8.GetByteCount(password) <= 72;
}
