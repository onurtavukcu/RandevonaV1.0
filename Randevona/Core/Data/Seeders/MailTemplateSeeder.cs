//namespace Data.Seeders
//{
//    public class MailTemplateSeeder
//    {
//        public static class MailTemplateMigrationSeeder // 
//        {
//            public static async Task SeedIfEmptyAsync(IMailTemplateRepository mailTemplateRepository)
//            {
//                var hasAnyTemplate = await mailTemplateRepository.ExistsAsync(x => true);
//                if (hasAnyTemplate)
//                {
//                    return;
//                }

//                var defaultTemplates = GetAllDefaultTemplates().ToList();
//                if (defaultTemplates.Count == 0)
//                {
//                    return;
//                }

//                await mailTemplateRepository.CreateManyAsync(defaultTemplates);
//            }

//            // Adds templates for any catalog keys that are not yet present, without touching existing rows.
//            public static async Task SeedMissingAsync(IMailTemplateRepository mailTemplateRepository)
//            {
//                var missing = new List<MailTemplate>();
//                foreach (var template in GetAllDefaultTemplates())
//                {
//                    var exists = await mailTemplateRepository.ExistsAsync(x => x.CatalogKey == template.CatalogKey);
//                    if (!exists)
//                    {
//                        missing.Add(template);
//                    }
//                }

//                if (missing.Count > 0)
//                {
//                    await mailTemplateRepository.CreateManyAsync(missing);
//                }
//            }

//            public static IEnumerable<MailTemplate> GetAllDefaultTemplates()
//            {
//                var keys = Enum.GetValues<MailCatalogKey>().Where(k => k != MailCatalogKey.Unknown);
//                foreach (var key in keys)
//                {
//                    var (subject, body) = GetTemplate(key);
//                    if (subject != null)
//                    {
//                        yield return new MailTemplate
//                        {
//                            CatalogKey = key,
//                            Version = 1,
//                            SubjectTemplate = subject,
//                            BodyHtmlTemplate = body ?? string.Empty
//                        };
//                    }
//                }
//            }

//            private static (string? Subject, string? BodyHtml) GetTemplate(MailCatalogKey mailCatalogKey)
//            {
//                switch (mailCatalogKey)
//                {
//                    case MailCatalogKey.Identity_UserApprovalRequest:
//                        return (
//                           "Yeni kayit onayi gerekiyor: {{UserEmail}} ({{TenantName}})",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Yeni kayit onayi</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
              
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">
//                                  Chatopya - Yeni Kayit Onayi
//                                </div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                             <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#eef6ff;color:#0c7cd5;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">
//                                  Onay Bekleniyor
//                                </div>
//                                 <p style=""margin:0 0 12px;"">
//                                  Merhaba SuperAdmin,
//                                </p>

//                                <p style=""margin:0 0 12px;"">
//                                  <strong>{{UserEmail}}</strong> kullanicisi 
//                                  <strong>{{TenantName}}</strong> tenant'i icin kayit oldu.
//                                </p>

//                                <p style=""margin:0 0 16px;"">
//                                  Lutfen onay verin:
//                                </p>

//                                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0 16px;"">
//                                  <tr>
//                                    <td style=""padding:0 10px 10px 0;"">
//                                      <a href=""{{ApproveUrl}}""
//                                         style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#0c7cd5;color:#ffffff;"">
//                                        Onayla
//                                      </a>
//                                    </td>
//                                    <td style=""padding:0 10px 10px 0;"">
//                                      <a href=""{{ApproveDemoUrl}}""
//                                         style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#10b981;color:#ffffff;"">
//                                        Demo Olarak Kabul Et
//                                      </a>
//                                    </td>
//                                    <td style=""padding:0 0 10px 0;"">
//                                      <a href=""{{RejectUrl}}""
//                                         style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#f0f3f8;color:#1f2d3d;"">
//                                        Reddet
//                                      </a>
//                                    </td>
//                                  </tr>
//                                </table>

//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">
//                                  Token son kullanma zamani: {{ExpireDate}} UTC
//                                </p>
//                              </div>

//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                Bu e-postayi beklemiyorsaniz, lutfen goz ardi edin.
//                              </div>

//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Identity_UserRegistrationPending:
//                        return (
//                            "Kaydınız alındı - Yönetici onayı bekleniyor",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Kaydınız alındı</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Kayıt Talebi Alındı</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#fffbeb;color:#d97706;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Yönetici Onayı Bekleniyor</div>
//                                <p style=""margin:0 0 12px;"">Merhaba <strong>{{UserName}}</strong>,</p>
//                                <p style=""margin:0 0 16px;""><strong>{{TenantName}}</strong> organizasyonu için sistem kayıt talebiniz başarıyla alınmıştır. Sistem güvenliği gereği hesabınız, şirket yöneticisi onayından geçtikten sonra aktif hale gelecektir.</p>
//                                <div style=""background:#f8fafc;border-left:4px solid #0c7cd5;padding:15px 20px;margin-bottom:24px;border-radius:0 8px 8px 0;font-size:14px;"">
//                                  Kaydınız onaylandığında size giriş yapabileceğinizi bildiren ikinci bir e-posta göndereceğiz. Lütfen beklemede kalın.
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Bu işlemi siz yapmadıysanız, lütfen bu e-postayı görmezden gelin.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                İyi günler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Identity_PasswordResetVerification:
//                        return (
//                            "Şifre Sıfırlama Doğrulama Kodu",
//                            @"<!DOCTYPE html>
//                        <html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#fff;color:#1f2d3d;font-size:15px;line-height:1.6;"">
//                          <div style=""max-width:560px;margin:0 auto;padding:24px;"">
//                            <h1 style=""margin:0 0 24px;font-size:22px;font-weight:700;color:#1f2d3d;"">Şifre Sıfırlama Doğrulama Kodu</h1>
//                            <p style=""margin:0 0 16px;"">Merhaba {{UserName}},</p>
//                            <p style=""margin:0 0 12px;"">Şifre sıfırlama talebiniz alınmıştır. Doğrulama kodunuz:</p>
//                            <p style=""margin:16px 0 24px;font-size:28px;font-weight:700;letter-spacing:6px;color:#0c7cd5;"">{{VerificationCode}}</p>
//                            <p style=""margin:0 0 16px;"">Bu kod {{ExpireMinutes}} dakika geçerlidir.</p>
//                            <p style=""margin:0 0 24px;color:#6b7280;"">Eğer bu talebi siz yapmadıysanız, lütfen bu e-postayı görmezden gelin.</p>
//                            <p style=""margin:0;"">İyi günler,<br/>Chatopya Ekibi</p>
//                          </div>
//                        </body></html>"
//                        );

//                    case MailCatalogKey.Identity_AccountApproved:
//                        return (
//                            "Hesabınız onaylandı - Giriş yapabilirsiniz",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Hesabınız onaylandı</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Hesap Onayı</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#ecfdf5;color:#047857;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Onaylandı</div>
//                                <p style=""margin:0 0 12px;"">Merhaba {{UserName}},</p>
//                                <p style=""margin:0 0 16px;""><strong>{{TenantName}}</strong> için hesabınız onaylandı. Artık giriş yapabilirsiniz.</p>
//                                <div style=""margin:24px 0 8px;"">
//                                  <a href=""{{LoginUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#0c7cd5;color:#fff;"">Giriş yap</a>
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Bu işlemi siz yapmadıysanız, lütfen bu e-postayı görmezden gelin.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                İyi günler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Identity_OrganizationInviteExisting:
//                        return (
//                            "{{OrganizationName}} organizasyonuna davet edildiniz",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Organizasyon Daveti</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Organizasyon Daveti</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#eef6ff;color:#0c7cd5;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Davet Bekleniyor</div>
//                                <p style=""margin:0 0 12px;"">Merhaba <strong>{{UserName}}</strong>,</p>
//                                <p style=""margin:0 0 16px;""><strong>{{TenantName}}</strong> bunyesindeki <strong>{{OrganizationName}}</strong> organizasyonuna davet edildiniz.</p>
//                                <div style=""margin:24px 0 8px;"">
//                                  <a href=""{{AcceptUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#0c7cd5;color:#fff;"">Daveti Kabul Et</a>
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Bu daveti siz talep etmediyseniz, lutfen bu e-postayi goz ardi edin.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                Iyi gunler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Identity_OrganizationInviteNewUser:
//                        return (
//                            "{{OrganizationName}} organizasyonuna hesabiniz olusturuldu",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Hesabiniz Olusturuldu</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Hesabiniz Olusturuldu</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#ecfdf5;color:#047857;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Hesap Hazir</div>
//                                <p style=""margin:0 0 12px;"">Merhaba <strong>{{UserName}}</strong>,</p>
//                                <p style=""margin:0 0 16px;""><strong>{{OrganizationName}}</strong> organizasyonu icin sizin adiniza bir hesap olusturuldu. Giris bilgileriniz:</p>
//                                <div style=""background:#f8fafc;border-left:4px solid #0c7cd5;padding:15px 20px;margin-bottom:24px;border-radius:0 8px 8px 0;font-size:14px;"">
//                                  E-posta: <strong>{{Email}}</strong><br/>
//                                  Sifre: <strong>{{Password}}</strong>
//                                </div>
//                                <div style=""margin:0 0 24px;"">
//                                  <a href=""{{LoginUrl}}"" style=""display:inline-block;padding:12px 24px;background:#0c7cd5;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;font-size:14px;"">Giris Yap</a>
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Guvenliginiz icin ilk girisinizde sifrenizi degistirmenizi oneririz.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                Iyi gunler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Template_TemplateCreated:
//                        return (
//                            "Meta şablonunuz oluşturuldu - Mesajınız gönderime hazır",
//                            @"<!DOCTYPE html>
//                        <html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;font-size:15px;line-height:1.6;"">
//                          <div style=""max-width:560px;margin:0 auto;padding:24px;"">
//                            <div style=""background:#fff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.06);padding:24px;"">
//                              <h1 style=""margin:0 0 16px;font-size:20px;font-weight:700;color:#1f2d3d;"">WhatsApp şablonunuz oluşturuldu</h1>
//                              <p style=""margin:0 0 12px;"">Merhaba,</p>
//                              <p style=""margin:0 0 16px;"">Meta (WhatsApp) tarafında mesaj şablonunuz başarıyla oluşturuldu. Mesajınız gönderime hazır.</p>
//                              <p style=""margin:0 0 8px;font-size:13px;color:#6b7280;"">Bu e-postayı beklemiyorsanız lütfen görmezden gelin.</p>
//                            </div>
//                          </div>
//                        </body></html>"
//                        );

//                    case MailCatalogKey.Identity_AccountRejected:
//                        return (
//                              "Hesabınız Oluşturma İsteğiniz Reddedildi.",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Oluşturma İsteğiniz Reddedildi.</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Hesap Onayı</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#ecfdf5;color:#047857;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Onaylandı</div>
//                                <p style=""margin:0 0 12px;"">Merhaba {{UserName}},</p>
//                                <p style=""margin:0 0 16px;""><strong>Hesap Oluşturma İsteğiniz Reddedildi.</strong> Reddedilme sebebi = {{Reason}}</p>
//                                <div style=""margin:24px 0 8px;"">                                  
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Bu işlemi siz yapmadıysanız, lütfen bu e-postayı görmezden gelin.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                İyi günler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>"
//                        );

//                    case MailCatalogKey.Template_DispatchAdminNotification:
//                        return (
//                            "[Bilgi] WhatsApp Şablon Gönderimi Başlatıldı",
//                            @"<!doctype html>
//                            <html lang=""tr"">
//                            <head>
//                              <meta charset=""utf-8"">
//                              <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                              <title>Şablon Bildirimi</title>
//                            </head>
//                            <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f0f2f5;color:#1e293b;"">
//                              <div style=""width:100%;background:#f0f2f5;padding:40px 20px;"">
//                                <div style=""max-width:600px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #eef0f2;"">
              
//                                  <div style=""background:linear-gradient(135deg,#0a2540 0%,#1579c1 50%,#03b6b6 100%);padding:25px 30px;"">
//                                    <div style=""font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;"">
//                                      Chatopya <span style=""font-weight:400;opacity:0.9;"">| Şablon Bildirimi</span>
//                                    </div>
//                                  </div>

//                                  <div style=""padding:35px 30px;"">

//                                    <div style=""display:inline-block;background:#e0f2fe;color:#0284c7;padding:6px 16px;border-radius:20px;font-size:13px;font-weight:600;margin-bottom:25px;border:1px solid #bae6fd;"">
//                                      Yeni Gönderim Talebi
//                                    </div>

//                                    <p style=""margin:0 0 12px;font-size:18px;font-weight:600;"">Merhaba,</p>

//                                    <p style=""color:#475569;font-size:15px;line-height:1.6;margin-bottom:25px;"">
//                                      Yeni bir WhatsApp şablon gönderim talebi alındı. Kullanıcı ve içerik detayları aşağıda yer almaktadır:
//                                    </p>

//                                    <div style=""background:#f8fafc;border-left:4px solid #1579c1;padding:15px 20px;margin-bottom:30px;border-radius:0 8px 8px 0;"">
//                                      <p style=""margin:0 0 8px;font-size:15px;"">
//                                        <strong>Kullanıcı Email:</strong> <span style=""color:#1579c1;"">{{UserId}}</span>
//                                      </p>
//                                      <p style=""margin:0;font-size:15px;"">
//                                        <strong>Organizasyon Email :</strong> {{TenantId}}
//                                      </p>
//                                    </div>

//                                    <h3 style=""font-size:16px;margin-bottom:12px;font-weight:600;"">Şablon İçeriği</h3>

//                                    <div style=""background:#ffffff;border:1px solid #e2e8f0;border-radius:8px;padding:18px;margin-bottom:30px;font-size:15px;line-height:1.6;"">
//                                      {{Content}}
//                                    </div>

//                                    <div style=""border-top:1px solid #e2e8f0;padding-top:20px;display:flex;justify-content:space-between;align-items:center;"">
//                                      <p style=""margin:0;font-size:12px;color:#94a3b8;"">
//                                        Zaman: <strong style=""color:#64748b;"">{{OperationTime}} UTC</strong>
//                                      </p>
//                                      <p style=""margin:0;font-size:12px;color:#94a3b8;text-align:right;"">
//                                        Bu otomatik bir mesajdır,<br/>lütfen cevaplamayınız.
//                                      </p>
//                                    </div>
//                                  </div>
//                                </div>
//                              </div>
//                            </body>
//                            </html>"
//                        );

//                    case MailCatalogKey.Identity_ForgotPassword:
//                        return ("Şifre Sıfırlama Doğrulama Kodunuz",
//                            @"
//                        <!doctype html>
//                        <html lang='tr'>
//                        <head>
//                            <meta charset='utf-8'>
//                            <meta name='viewport' content='width=device-width, initial-scale=1'>
//                            <title>Şifre Sıfırlama</title>
//                        </head>

//                        <body style='margin:0;padding:0;background:#f6f8fb;font-family:Arial,Helvetica,sans-serif;'>

//                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' 
//                               style='background:#f6f8fb;padding:24px 0;'>

//                        <tr>
//                        <td align='center'>

//                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
//                               style='max-width:520px;background:#ffffff;
//                                      border-radius:12px;
//                                      box-shadow:0 8px 24px rgba(15,23,42,0.08);
//                                      padding:32px;'>

//                        <tr>
//                        <td style='text-align:center;'>

//                        <h2 style='margin:0 0 16px 0;
//                                   color:#1f2d3d;
//                                   font-size:22px;
//                                   font-weight:600;'>

//                        Şifre Sıfırlama Doğrulama Kodu

//                        </h2>

//                        <p style='margin:0 0 16px 0;
//                                  color:#4a5568;
//                                  font-size:14px;'>

//                        Merhaba,

//                        </p>

//                        <p style='margin:0 0 24px 0;
//                                  color:#4a5568;
//                                  font-size:14px;
//                                  line-height:1.6;'>

//                        Şifre sıfırlama talebiniz alınmıştır.  
//                        Aşağıdaki doğrulama kodunu kullanarak işleminizi tamamlayabilirsiniz.

//                        </p>

//                        <div style='
//                            margin:24px 0;
//                            padding:18px;
//                            background:#f1f5ff;
//                            border-radius:10px;
//                            text-align:center;
//                        '>

//                        <span style='
//                            display:inline-block;
//                            color:#066fd1;
//                            font-size:34px;
//                            font-weight:700;
//                            letter-spacing:6px;
//                            font-family:Courier New, monospace;
//                        '>

//                        {{verificationCode}}

//                        </span>

//                        </div>

//                        <p style='margin:0 0 16px 0;
//                                  color:#4a5568;
//                                  font-size:13px;'>

//                        Bu kod <strong>5 dakika</strong> geçerlidir.

//                        </p>

//                        <p style='margin:0 0 16px 0;
//                                  color:#4a5568;
//                                  font-size:13px;'>

//                        Eğer bu talebi siz yapmadıysanız,  
//                        lütfen bu email'i görmezden gelin.

//                        </p>

//                        <hr style='border:none;
//                                   border-top:1px solid #e2e8f0;
//                                   margin:24px 0;'>

//                        <p style='margin:0;
//                                  color:#94a3b8;
//                                  font-size:12px;'>

//                        Bu email otomatik olarak gönderilmiştir.  
//                        Yanıtlamanıza gerek yoktur.

//                        </p>

//                        <p style='margin:8px 0 0 0;
//                                  color:#1f2d3d;
//                                  font-size:13px;
//                                  font-weight:600;'>

//                        Chatopya Ekibi

//                        </p>

//                        </td>
//                        </tr>

//                        </table>

//                        </td>
//                        </tr>

//                        </table>

//                        </body>
//                        </html>");

//                    case MailCatalogKey.Identity_ResetPassword:
//                        return ("Şifreniz Başarıyla Sıfırlandı",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head>
//                          <meta charset=""utf-8"">
//                          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
//                          <title>Şifreniz Sıfırlandı</title>
//                        </head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Şifreniz Güncellendi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#ecfdf5;color:#047857;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Başarılı İşlem</div>
//                                <p style=""margin:0 0 12px;"">Merhaba <strong>{{UserName}}</strong>,</p>
//                                <p style=""margin:0 0 16px;"">Hesabınızın şifresi başarıyla sıfırlandı ve yeni şifreniz güncellendi.</p>
//                                <div style=""background:#f8fafc;border-left:4px solid #10b981;padding:15px 20px;margin-bottom:24px;border-radius:0 8px 8px 0;font-size:14px;"">
//                                  Hesabınıza hemen giriş yapabilirsiniz. Eğer bu değişikliği siz yapmadıysanız lütfen acilen yöneticinizle iletişime geçin.
//                                </div>
//                                <div style=""margin:0 0 24px;"">
//                                  <a href=""{{LoginUrl}}"" style=""display:inline-block;padding:12px 24px;background:#0c7cd5;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;font-size:14px;"">Şimdi Giriş Yap</a>
//                                </div>
//                                <p style=""margin:16px 0 0;font-size:12px;color:#6b7280;"">Giren IP adresi ile ilgili kayıtlar sistemimizde güvenliğiniz için loglanmaktadır.</p>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">
//                                İyi günler,<br/>Chatopya Ekibi
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_ApprovalRequest:
//                        return (
//                            "Abonelik onayi gerekiyor: {{PlanName}} ({{TenantName}})",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Abonelik onayi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#0c7cd5,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Abonelik Onayi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#eef6ff;color:#0c7cd5;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Onay Bekleniyor</div>
//                                <p style=""margin:0 0 12px;"">Merhaba SuperAdmin,</p>
//                                <p style=""margin:0 0 12px;""><strong>{{TenantName}}</strong> tenant'inin <strong>{{OrganizationName}}</strong> organizasyonu <strong>{{PlanName}}</strong> paketine ({{BillingCycle}}) abone olmak istiyor.</p>
//                                <p style=""margin:0 0 12px;"">Tutar: <strong>{{Price}} {{Currency}}</strong></p>
//                                <p style=""margin:0 0 16px;"">Lutfen onay verin:</p>
//                                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0 16px;"">
//                                  <tr>
//                                    <td style=""padding:0 10px 10px 0;"">
//                                      <a href=""{{ApproveUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#0c7cd5;color:#ffffff;"">Onayla</a>
//                                    </td>
//                                    <td style=""padding:0 0 10px 0;"">
//                                      <a href=""{{RejectUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#f0f3f8;color:#1f2d3d;"">Reddet</a>
//                                    </td>
//                                  </tr>
//                                </table>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">Bu e-postayi beklemiyorsaniz, lutfen goz ardi edin.</div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_Approved:
//                        return (
//                            "Aboneliginiz onaylandi: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Abonelik onaylandi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#10b981,#0fb8ad);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Abonelik Onaylandi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonunun <strong>{{PlanName}}</strong> paketine aboneligi onaylandi ve aktif edildi.</p>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_Rejected:
//                        return (
//                            "Abonelik talebiniz reddedildi: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Abonelik reddedildi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#ef4444,#f59e0b);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Abonelik Reddedildi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonunun <strong>{{PlanName}}</strong> paketine abonelik talebi onaylanmadi.</p>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_ExpiringSoon:
//                        return (
//                            "Aboneliginiz {{DaysRemaining}} gun icinde sona eriyor: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Abonelik suresi yaklasiyor</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#f59e0b,#ef4444);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Abonelik Suresi Yaklasiyor</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonunun <strong>{{PlanName}}</strong> paketi <strong>{{DaysRemaining}} gun</strong> icinde ({{EndDate}}) sona eriyor.</p>
//                                <p style=""margin:0 0 12px;"">Kesintisiz erisim icin aboneliginizi yenilemenizi oneririz.</p>
//                                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0 16px;"">
//                                  <tr>
//                                    <td>
//                                      <a href=""{{RenewUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#f59e0b;color:#ffffff;"">Aboneligi Yenile</a>
//                                    </td>
//                                  </tr>
//                                </table>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_Expired:
//                        return (
//                            "Aboneliginiz sona erdi: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Abonelik sona erdi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#ef4444,#7c3aed);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Aboneliginiz Sona Erdi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonunun <strong>{{PlanName}}</strong> paketinin suresi doldu ve ilgili ozellikler artik kullanilamiyor.</p>
//                                <p style=""margin:0 0 12px;"">Erisimi yeniden acmak icin aboneliginizi yenileyin.</p>
//                                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0 16px;"">
//                                  <tr>
//                                    <td>
//                                      <a href=""{{RenewUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#ef4444;color:#ffffff;"">Aboneligi Yenile</a>
//                                    </td>
//                                  </tr>
//                                </table>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Subscription_EnterpriseContactRequest:
//                        return (
//                            "Yeni Enterprise teklif talebi: {{PlanName}} ({{TenantName}})",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Enterprise teklif talebi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#7c3aed,#0c7cd5);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Enterprise Teklif Talebi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba SuperAdmin,</p>
//                                <p style=""margin:0 0 12px;""><strong>{{TenantName}}</strong> tenant'inin <strong>{{OrganizationName}}</strong> organizasyonu <strong>{{PlanName}}</strong> paketi icin fiyat teklifi talep etti.</p>
//                                <p style=""margin:0 0 6px;"">E-posta: <strong>{{RequesterEmail}}</strong></p>
//                                <p style=""margin:0 0 6px;"">Telefon: <strong>{{RequesterPhone}}</strong></p>
//                                <p style=""margin:0 0 12px;"">Mesaj:</p>
//                                <p style=""margin:0 0 12px;padding:12px;background:#f6f8fb;border-radius:8px;white-space:pre-wrap;"">{{RequesterMessage}}</p>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Addon_ApprovalRequest:
//                        return (
//                            "Ek ozellik onayi gerekiyor: {{PlanName}} ({{TenantName}})",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Ek ozellik onayi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#7c3aed,#0c7cd5);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;letter-spacing:0.3px;"">Chatopya - Ek Ozellik Onayi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <div style=""display:inline-block;padding:6px 10px;border-radius:999px;background:#f3eeff;color:#7c3aed;font-weight:600;font-size:12px;letter-spacing:0.2px;margin-bottom:16px;"">Onay Bekleniyor</div>
//                                <p style=""margin:0 0 12px;"">Merhaba SuperAdmin,</p>
//                                <p style=""margin:0 0 12px;""><strong>{{TenantName}}</strong> tenant'inin <strong>{{OrganizationName}}</strong> organizasyonu <strong>{{PlanName}}</strong> ek ozelligini ({{BillingCycle}}) satin almak istiyor.</p>
//                                <p style=""margin:0 0 12px;"">Tutar: <strong>{{Price}} {{Currency}}</strong></p>
//                                <p style=""margin:0 0 16px;"">Lutfen onay verin:</p>
//                                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0 16px;"">
//                                  <tr>
//                                    <td style=""padding:0 10px 10px 0;"">
//                                      <a href=""{{ApproveUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#7c3aed;color:#ffffff;"">Onayla</a>
//                                    </td>
//                                    <td style=""padding:0 0 10px 0;"">
//                                      <a href=""{{RejectUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:10px;font-weight:600;font-size:14px;text-decoration:none;text-align:center;background:#f0f3f8;color:#1f2d3d;"">Reddet</a>
//                                    </td>
//                                  </tr>
//                                </table>
//                              </div>
//                              <div style=""padding:0 24px 24px;font-size:12px;color:#6b7280;line-height:1.5;"">Bu e-postayi beklemiyorsaniz, lutfen goz ardi edin.</div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Addon_Approved:
//                        return (
//                            "Ek ozelliginiz onaylandi: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Ek ozellik onaylandi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#10b981,#7c3aed);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Ek Ozellik Onaylandi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonu icin <strong>{{PlanName}}</strong> ek ozelliginin aktivasyonu onaylandi ve hesabiniza eklendi.</p>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    case MailCatalogKey.Addon_Rejected:
//                        return (
//                            "Ek ozellik talebiniz reddedildi: {{PlanName}}",
//                            @"<!doctype html>
//                        <html lang=""tr"">
//                        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>Ek ozellik reddedildi</title></head>
//                        <body style=""margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#1f2d3d;"">
//                          <div style=""width:100%;background:#f6f8fb;padding:32px 0;"">
//                            <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 8px 24px rgba(15,23,42,0.12);overflow:hidden;"">
//                              <div style=""background:linear-gradient(135deg,#ef4444,#7c3aed);padding:20px 24px;color:#fff;"">
//                                <div style=""font-size:18px;font-weight:700;"">Chatopya - Ek Ozellik Reddedildi</div>
//                              </div>
//                              <div style=""padding:24px;line-height:1.6;font-size:15px;"">
//                                <p style=""margin:0 0 12px;"">Merhaba {{TenantName}},</p>
//                                <p style=""margin:0 0 12px;""><strong>{{OrganizationName}}</strong> organizasyonu icin <strong>{{PlanName}}</strong> ek ozellik talebiniz onaylanmadi.</p>
//                              </div>
//                            </div>
//                          </div>
//                        </body>
//                        </html>");

//                    default:
//                        return (null, null);
//                }
//            }
//        }
//    }
//}