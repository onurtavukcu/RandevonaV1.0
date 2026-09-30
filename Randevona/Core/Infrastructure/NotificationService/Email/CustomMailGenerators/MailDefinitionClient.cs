//using Domain.Models.Notification.Email;
//using Infrastructure.NotificationServices.Email.BaseEmailService;
//using Infrastructure.NotificationServices.Email.DefinitionResolver;
//using Microsoft.Extensions.Logging;

//namespace Infrastructure.NotificationServices.Email.CustomMailGenerators
//{
//    public class MailDefinitionClient : IMailDefinitionClient
//    {
//        private readonly IMailDefinitionResolver _mailDefinitionResolver;
//        private readonly IEmailServices _mailService;
//        private readonly ILogger<MailDefinitionClient> _logger;

//        public MailDefinitionClient(
//            IMailDefinitionResolver mailDefinitionResolver,
//            IEmailServices mailService,
//            ILogger<MailDefinitionClient> logger)
//        {
//            _mailDefinitionResolver = mailDefinitionResolver ?? throw new ArgumentNullException(nameof(mailDefinitionResolver));
//            _mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task SendByMailDefinitionAsync(SendMailByDefinitionRequest request, CancellationToken ct)
//        {
//            if (request == null)
//                throw new ArgumentNullException(nameof(request));
//            if (!request.To.Any())
//                throw new ArgumentException("To is required.", nameof(request));
//            if (request.MailCatalogKey == MailCatalogKey.Unknown)
//                throw new ArgumentException("MailCatalogKey is required.", nameof(request));
//            if (string.IsNullOrWhiteSpace(request.SourceService))
//                throw new ArgumentException("SourceService is required.", nameof(request));

//            _logger.LogInformation(
//                "Resolving and sending mail by definition. SourceService={SourceService}, MailCatalogKey={MailCatalogKey}, To={To}",
//                request.SourceService, request.MailCatalogKey, request.To);

//            var variables = request.Variables ?? new Dictionary<string, string>();

//            var result = await _mailDefinitionResolver.ResolveTemplateAsync(request.MailCatalogKey, variables);
//            if (!result.Success)
//            {
//                throw new InvalidOperationException($"Unknown or unsupported MailCatalogKey: {request.MailCatalogKey}");
//            }
//            var subject = result.Subject;
//            var bodyHtml = result.BodyHtml;

//            var message = new EmailMessage
//            {
//                To = request.To,
//                Subject = subject,
//                Body = bodyHtml,
//                IsHtml = true,
//                MailCatalogKey = request.MailCatalogKey,
//                Variables = variables
//            };

//            await _mailService.SendeEmailAsync(message, ct);
//        }
//    }
//}
