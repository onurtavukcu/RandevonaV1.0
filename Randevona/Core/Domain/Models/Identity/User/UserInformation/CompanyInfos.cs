namespace Domain.Models.Identity.User.UserInformation
{
    public class CompanyInfos
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? LegalName { get; set; }
        public string? CompanyDescription { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyPhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
    }
}
