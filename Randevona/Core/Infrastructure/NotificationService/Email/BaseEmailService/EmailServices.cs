//using Domain.Models.Notification.Email;
//using Domain.Models.Shared.Result;
//using System.Net;
//using System.Net.Mail;

//namespace Infrastructure.NotificationServices.Email.BaseEmailService
//{
//    public class EmailServices : IEmailServices
//    {
//        private readonly SmtpSettings _smtpSettings;

//        public EmailServices(SmtpSettings smtpSettings)
//        {
//            _smtpSettings = smtpSettings;
//        }

//        public async Task<Result> SendeEmailAsync(EmailMessage message, CancellationToken ct)
//        {
//            ArgumentNullException.ThrowIfNull(message);

//            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) ||
//                string.IsNullOrWhiteSpace(_smtpSettings.FromAddress) ||
//                string.IsNullOrWhiteSpace(_smtpSettings.UserName) ||
//                string.IsNullOrWhiteSpace(_smtpSettings.Password))
//            {
//                return new Error("Email.SmtpNotConfigured", "Missing SMTP Configuration!", ErrorType.Failure);
//            }

//            using var mailMessage = new MailMessage
//            {
//                From = new MailAddress(_smtpSettings.FromAddress, "Randevona"),
//                Subject = message.Subject,
//                Body = message.Body,
//                IsBodyHtml = message.IsHtml
//            };

//            if (message.To is null || !message.To.Any())
//                return new Error("Email.NoRecipient", "At least one buyer is required!", ErrorType.Validation);

//            foreach (var to in message.To)
//            {
//                if (!MailAddress.TryCreate(to, out var address))
//                    return new Error("Email.InvalidRecipient", $"Geçersiz adres: {to}", ErrorType.Validation);

//                mailMessage.To.Add(address);
//            }
//            try
//            {
//                using var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
//                {
//                    EnableSsl = true,
//                    UseDefaultCredentials = false,
//                    Credentials = new NetworkCredential(_smtpSettings.UserName, _smtpSettings.Password),
//                    DeliveryMethod = SmtpDeliveryMethod.Network,
//                    Timeout = 60000
//                };

//                await smtpClient.SendMailAsync(mailMessage, ct);
//                return Result.Success();
//            }
//            catch (OperationCanceledException) when (ct.IsCancellationRequested)
//            {
//                throw;
//            }
//            catch (SmtpException ex)
//            {
//                return new Error("Email.SmtpError", ex.Message, ErrorType.Failure, ex);
//            }
//        }
//    }
//}