//using Domain.Models.Notification.Email;
//using System.Text;
//using Domain.Interfaces.Notification;
//using System.Text.Json;

//namespace Infrastructure.NotificationServices.Email.DefinitionResolver
//{
//    public class MailDefinitionResolver : IMailDefinitionResolver
//    {
//        private readonly IMailTemplateRepository _mailTemplateRepository;

//        public MailDefinitionResolver(IMailTemplateRepository mailTemplateRepository)
//        {
//            _mailTemplateRepository = mailTemplateRepository;
//        }
//        public async Task<MailDefinitionResult> ResolveTemplateAsync(MailCatalogKey mailCatalogKey, IReadOnlyDictionary<string, string> variables)
//        {
//            if (mailCatalogKey == MailCatalogKey.Unknown)
//                return new MailDefinitionResult { Success = false };

//            string? templateSubject = null;
//            string? templateBodyHtml = null;

//            // Try DB first
//            var mailTemplate = await _mailTemplateRepository.GetActiveTemplateAsync(mailCatalogKey);
//            if (mailTemplate != null)
//            {
//                templateSubject = mailTemplate.SubjectTemplate;
//                templateBodyHtml = mailTemplate.BodyHtmlTemplate;
//            }

//            if (templateSubject == null)
//                return new MailDefinitionResult { Success = false };

//            var resolvedVariables = new Dictionary<string, string>(variables);

//            if (mailCatalogKey == MailCatalogKey.Template_DispatchAdminNotification)
//            {
//                if (variables.TryGetValue("JsonData", out var jsonData))
//                {
//                    resolvedVariables["Content"] = FormatTemplateJson(jsonData);
//                }

//                if (!resolvedVariables.ContainsKey("OperationTime"))
//                {
//                    resolvedVariables["OperationTime"] = DateTime.UtcNow.ToString("dd MMM yyyy, HH:mm");
//                }
//            }

//            var subject = ResolvePlaceholders(templateSubject, resolvedVariables);
//            var bodyHtml = ResolvePlaceholders(templateBodyHtml ?? string.Empty, resolvedVariables);

//            return new MailDefinitionResult
//            {
//                Success = true,
//                Subject = subject,
//                BodyHtml = bodyHtml
//            };
//        }

//        private static string ResolvePlaceholders(string template, IReadOnlyDictionary<string, string> variables)
//        {
//            if (variables == null || variables.Count == 0)
//                return template;
//            var sb = new StringBuilder(template);
//            foreach (var kv in variables)
//            {
//                var placeholder = "{{" + kv.Key + "}}";
//                sb.Replace(placeholder, kv.Value ?? string.Empty);
//            }
//            return sb.ToString();
//        }



//        private static string FormatTemplateJson(string jsonData)
//        {
//            if (string.IsNullOrWhiteSpace(jsonData)) return "";

//            var formattedData = "";
//            try
//            {
//                using var templateDataObj = JsonDocument.Parse(jsonData);

//                if (templateDataObj.RootElement.TryGetProperty("templateName", out var tName))
//                {

//                    formattedData += $"<h3>Şablon Adı: <span style='color:#1579c1;'>{tName.GetString()}</span></h3><hr/>";
//                }

//                if (templateDataObj.RootElement.TryGetProperty("components", out var components))
//                {
//                    foreach (var comp in components.EnumerateArray())
//                    {
//                        var typeStr = comp.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "UNKNOWN";

//                        var label = (typeStr ?? "UNKNOWN").ToUpperInvariant() switch
//                        {
//                            "HEADER" => "Başlık Bölümü",
//                            "BODY" => "Mesaj İçeriği",
//                            "FOOTER" => "Alt Bilgi",
//                            "BUTTONS" => "Butonlar",
//                            _ => typeStr
//                        };

//                        if (comp.TryGetProperty("parameters", out var parameters))
//                        {
//                            foreach (var param in parameters.EnumerateArray())
//                            {
//                                var pTypeStr = param.TryGetProperty("type", out var ptProp) ? ptProp.GetString() : "";

//                                if (pTypeStr == "text" && param.TryGetProperty("text", out var text))
//                                {
//                                    formattedData += $"<p><strong>{label}:</strong><br/> \"{text.GetString()}\"</p>";
//                                }
//                                else if (pTypeStr == "image" && param.TryGetProperty("image", out var img))
//                                {
//                                    if (img.TryGetProperty("link", out var link))2 //TODO email service yapılacak
//                                    {
//                                        formattedData += $"<p><strong>Görsel İçerik:</strong><br/> <img src='{link.GetString()}' style='max-width:300px; border-radius:8px; border:1px solid #e2e8f0; margin-top:10px;' /></p>";
//                                    }
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//            catch
//            {
//                formattedData = $"<p><em>(Ham Veri)</em></p><pre>{jsonData}</pre>";
//            }

//            return formattedData;
//        }
//    }
//}
