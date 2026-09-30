namespace Domain.Models.Notification.Email
{
    public enum MailCatalogKey
    {
        Unknown = 0,
        Identity_UserApprovalRequest = 1,
        Identity_UserRegistrationPending = 2,
        Identity_PasswordResetVerification = 3,
        Identity_AccountApproved = 4,
        Identity_AccountRejected = 5,
        Identity_ForgotPassword = 6,
        Identity_ResetPassword = 7,
        Template_DispatchAdminNotification = 100,
        Subscription_ApprovalRequest = 300,
        Subscription_Approved = 301,
        Subscription_Rejected = 302,
        Subscription_ExpiringSoon = 320,
        Subscription_Expired = 321,
        Subscription_EnterpriseContactRequest = 330,
    }
}
