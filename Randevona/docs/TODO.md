# TODO

Bu liste, mevcut geliştirme adımından ayrı tutulan ertelenmiş işlerdir.

- [ ] **CRM kullanan firmalarda aynı saate rezervasyonu engelleme ve mutabakat:** Son onayda CRM'nin rezervasyonu atomik kabul etmesini sağla. Bizden bağımsız oluşturulan/değişen/iptal edilen randevuların senkronizasyonunu, veri sahipliğini ve saklama koşullarını firma ile belirle. Test/pilot öncesinde yeniden değerlendir.
- [ ] **İlk iletişim ve mesaj izinleri:** Excel contact listesindeki müşterilere WhatsApp'a geçiş duyurusunun hangi izinle yapılacağını; izin toplama, ispat, geri çekme ve iletişimden çıkma süreçlerini değerlendir. Test/pilot öncesinde yeniden değerlendir.
- [ ] **Uygulama güvenliği ve veri saklama:** Türkiye'deki entegrasyon/veri saklama gereklilikleri ve Meta politikalarına göre şifrelenecek alanları, saklama/silme sürelerini, erişim denetimini, logları ve anahtar yönetimini belirle. Mevcut şifreleme bu çalışmanın tamamlandığı anlamına gelmez.
- [ ] **E-posta servisi:** SMTP/sağlayıcı bağlantısı, mail şablonları ve gönderim hata/tekrar yönetimini tamamla. Register e-posta doğrulaması, başvuru/onay/red bildirimleri, forgot-password kodu ve şifre sıfırlama akışlarını bağla. E-posta adresi doğrulaması ile superadmin onayı ayrı kontrollerdir.
- [x] **Superadmin kayıt onayı:** Tenant/kullanıcı listeleri ve detayları, bekleyen başvurularda onay/ret, zorunlu ret gerekçesi, karar veren kişi/zaman kaydı tamamlandı. Onayda tenant hazırlığı yeniden denenir; hazır tenant ve erişilebilir şube olmadan hesap açılmaz. Eski/eşzamanlı kararlar önceki kararı değiştiremez. E-posta bildirimi ayrı TODO olarak kalır.

## Register aşamasında uygulanan temel

Yeni hesap normal User rolünde ve PendingApproval durumunda oluşturulur. Kullanıcı ve tenant merkez DB'de aynı transaction ile kaydedilir. Tenant DB/ilk şube hazırlığı ayrı, tekrar edilebilir adımdır; hazırlanmış tenant, kullanıcının onaylandığı anlamına gelmez. Register otomatik giriş veya e-posta gönderimi yapmaz. Onay/red yönetimi ve e-posta teslimi yukarıdaki TODO maddeleridir.

## Login denetiminde kalan işler — 01.10.2026

- [ ] **Giriş/kayıt denemesi sınırları:** Endpoint hız sınırı, hesap başına başarısız giriş takibi ve tekrar deneme politikasını uygula; proxy arkasında gerçek istemci adresini yalnız güvenilir proxy üzerinden çöz.
- [ ] **Parola değişiminde oturum iptali:** Kullanıcı oturum sürümü/security stamp ile mevcut cookie ve JWT oturumlarını geçersiz kıl. Şu an hesap durumu ve rol değişimi kontrol ediliyor, PasswordHash değişimi mevcut oturumu iptal etmiyor.
- [x] **Superadmin ilk kurulumu:** SystemAdminSettings ile merkez Users içinde tenantsız Active/SuperAdmin oluşturulur. Tenant/şube/DB oluşturulmaz. Ayarlı aktif eski adminlerin tenant ve şube bağlantısı kaldırılır; mevcut DB ve tenant kayıtları korunur. Mevcut parola, rol ve hesap durumu değiştirilmez.
- [ ] **Superadmin yönetim bölümü:** Tenantsız giriş, tenant/kullanıcı liste ve detayları, başvuru onay/ret tamamlandı. Platform Settings / Package Settings / Reports sayfaları boş iskelet. Ayar/paket/rapor iş mantığı ve açık yetkilendirmeyle müşteri tenant seçimi henüz yapılmadı. Aktif hesap askıya alma/yeniden açma ve reddedilmiş başvuruyu yeniden değerlendirme bu adımın kapsamında değil.

Login, onay bekleyen/reddedilmiş/kapalı hesapları reddeder. Yeni kayıtların otomatik aktif yapılması eklenmedi. E-posta servisi ve onay/red ekranları ertelenmiş durumdadır.

## Application language — 2026-10-02

All application UI text, validation messages, error codes and service responses must be in English. Active account/home views and register/password messages have been updated; existing English login messages are retained. The Web request culture is en-US. User-entered names and the business time zone remain domain data. When the currently commented-out legacy email templates are implemented, use English Randevona content instead of the old templates.




