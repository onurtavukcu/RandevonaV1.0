# TODO

Bu liste, mevcut geliştirme adımından ayrı tutulan ertelenmiş işlerdir.

- [ ] **CRM kullanan firmalarda aynı saate rezervasyonu engelleme ve mutabakat:** Son onayda CRM'nin rezervasyonu atomik kabul etmesini sağla. Bizden bağımsız oluşturulan/değişen/iptal edilen randevuların senkronizasyonunu, veri sahipliğini ve saklama koşullarını firma ile belirle. Test/pilot öncesinde yeniden değerlendir.
- [ ] **İlk iletişim ve mesaj izinleri:** Excel contact listesindeki müşterilere WhatsApp'a geçiş duyurusunun hangi izinle yapılacağını; izin toplama, ispat, geri çekme ve iletişimden çıkma süreçlerini değerlendir. Test/pilot öncesinde yeniden değerlendir.
- [ ] **Uygulama güvenliği ve veri saklama:** Türkiye'deki entegrasyon/veri saklama gereklilikleri ve Meta politikalarına göre şifrelenecek alanları, saklama/silme sürelerini, erişim denetimini, logları ve anahtar yönetimini belirle. Mevcut şifreleme bu çalışmanın tamamlandığı anlamına gelmez.
- [ ] **E-posta servisi:** SMTP/sağlayıcı bağlantısı, mail şablonları ve gönderim hata/tekrar yönetimini tamamla. Register e-posta doğrulaması, başvuru/onay/red bildirimleri, forgot-password kodu ve şifre sıfırlama akışlarını bağla. E-posta adresi doğrulaması ile superadmin onayı ayrı kontrollerdir.
- [x] **Superadmin kayıt onayı:** Tenant/kullanıcı listeleri ve detayları, bekleyen başvurularda onay/ret, zorunlu ret gerekçesi, karar veren kişi/zaman kaydı tamamlandı. Onayda tenant hazırlığı yeniden denenir; hazır tenant ve erişilebilir şube olmadan hesap açılmaz. Eski/eşzamanlı kararlar önceki kararı değiştiremez. E-posta bildirimi ayrı TODO olarak kalır.

## Register aşamasında uygulanan temel

Yeni hesap normal User rolünde ve PendingApproval durumunda oluşturulur. Kullanıcı ve tenant merkez DB'de aynı transaction ile kaydedilir. Tenant DB/ilk şube hazırlığı ayrı, tekrar edilebilir adımdır; hazırlanmış tenant, kullanıcının onaylandığı anlamına gelmez. Register otomatik giriş veya e-posta gönderimi yapmaz. Onay/red yönetimi tamamlandı; e-posta teslimi yukarıdaki TODO maddesidir.

## Login denetiminde kalan işler — 01.10.2026

- [ ] **Giriş/kayıt denemesi sınırları:** Endpoint hız sınırı, hesap başına başarısız giriş takibi ve tekrar deneme politikasını uygula; proxy arkasında gerçek istemci adresini yalnız güvenilir proxy üzerinden çöz.
- [ ] **Parola değişiminde oturum iptali:** Kullanıcı oturum sürümü/security stamp ile mevcut cookie ve JWT oturumlarını geçersiz kıl. Şu an hesap durumu ve rol değişimi kontrol ediliyor, PasswordHash değişimi mevcut oturumu iptal etmiyor.
- [x] **Superadmin ilk kurulumu:** SystemAdminSettings ile merkez Users içinde tenantsız Active/SuperAdmin oluşturulur. Tenant/şube/DB oluşturulmaz. Ayarlı aktif eski adminlerin tenant ve şube bağlantısı kaldırılır; mevcut DB ve tenant kayıtları korunur. Mevcut parola, rol ve hesap durumu değiştirilmez.
- [ ] **Superadmin yönetim bölümü:** Tenantsız giriş, tenant/kullanıcı liste ve detayları, başvuru onay/ret tamamlandı. Platform Settings / Package Settings / Reports sayfaları boş iskelet. Açık işletme/şube seçimi tamamlandı. Ayar/paket/rapor iş mantığı henüz yapılmadı. Aktif hesap askıya alma/yeniden açma ve reddedilmiş başvuruyu yeniden değerlendirme bu adımın kapsamında değil.

Login, onay bekleyen/reddedilmiş/kapalı hesapları reddeder. Yeni kayıtların otomatik aktif yapılması eklenmedi. Onay/red ekranları tamamlandı; e-posta servisi ertelenmiş durumdadır.

## Application language — 2026-10-02

All application UI text, validation messages, error codes and service responses must be in English. Active account/home views and register/password messages have been updated; existing English login messages are retained. The Web request culture is en-US. User-entered names and the business time zone remain domain data. When the currently commented-out legacy email templates are implemented, use English Randevona content instead of the old templates.





## WhatsApp bağlantısı — 06.10.2026

- [x] SuperAdmin için onaylı işletme/aktif şube seçimi ve seçimi bırakma; gerçek yönetici kimliği korunur.
- [x] Connections numara listesi, manuel kayıt, token yenileme; tenant DB içinde şifreli token, merkez DB içinde benzersiz numara sahipliği.
- [x] İşletme/şube değişmiş veya eski sürümle gönderilmiş formları reddetme; numara ve merkez dizinini aynı transaction ile kaydetme.
- [x] Meta WABA numara listesinden hesap erişimini kontrol etme; API sürümü boşken dış çağrı kapalı. Bu işlem numara kaydı/mesaj gönderimi doğrulaması değildir.
- [ ] Gerçek Mongo replica-set üzerinde transaction/benzersiz index/yeniden deneme testleri ve kendi Meta test hesabıyla erişim kontrolü.
- [ ] Embedded Signup, Meta app/token kimliği, numara kayıt ve webhook aboneliği; sahiplik değişimi/bağlantı kaldırma akışları.
- [ ] Tek numaranın birden fazla randevu şubesine hizmet etmesi; mevcut bağlantı bu aşamada tek şubeye aittir.
- [ ] İmzalı webhook ve merkezi numara dizininden güvenli tenant çözümleme. Dizin henüz dışarıdan erişilen bir endpoint sağlamaz.

Ayrıntı ve sonraki sıra: WhatsApp-Connections.md. Testler gerçek Atlas'a veya Meta hesabına yazmadı.
