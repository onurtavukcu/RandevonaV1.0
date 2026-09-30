namespace Domain.Models.Identity.User.UserInformation
{
    public class OrganizationInfos
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string? OrganizationDescription { get; set; }
        public string? OrganizationAddress { get; set; }
        public string? OrganizationPhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
    }
}
