# Tenant başına veritabanı — karar ve uygulama notları

30.09.2026. Kullanıcının seçimi: merkez hesap veritabanı + işletme (tenant) başına ayrı veritabanı. Kullanıcı veya şube başına DB açılmaz. Bu değişiklik mevcut kayıtları taşımaz.

## Yerleşim

- MongoSettings.DatabaseName: merkez/kontrol veritabanının adı. Mevcut ayar anahtarı korunmuştur.
- Merkez koleksiyonları: Users ve Tenants. İleride platform abonelikleri ve WhatsApp numarası → tenant eşlemesi de bu sınırda tasarlanacak; henüz eklenmedi.
- İşletme koleksiyonları: Organizations, Employees; ileride randevu, contact, mesaj ve diğer iş verileri.
- Yeni tenant DB adı sunucuda <TenantDatabasePrefix>_<ana-şirket-adı> olarak üretilir ve Tenants.DatabaseName üzerinde tutulur. Örnek: randevona_dev_onurcomp. Ad yalnız formdaki Business name → CompanyInfos.CompanyName alanından sadeleştirilir. Şube adı veya tenant kimliği eklenmez. Aynı normalize DB adı ikinci bir tenant için kullanılamaz. İstekten doğrudan DB adı alınmaz.
- İsimler aynı MongoClient/cluster altında mantıksal veritabanlarıdır. Ayrı donanım, tenant'a özel Mongo kimlik bilgileri veya bağımsız yedekleme garantisi uygulanmış değildir.

## Servis ayrımı

- IControlMongoDbContext: yalnız merkez Users/Tenants koleksiyonları. Repository<T> bununla çalışır.
- IMongoDbContext: yalnız TenantBaseEntity iş modelleri; GetCollectionAsync tenant veritabanını çözer.
- ITenantDatabaseResolver: güvenilir WorkContext.TenantId ile merkez kaydı okur. Aktif olmayan tenant, eksik/uyuşmayan DB eşlemesi reddedilir; merkez DB'ye geri dönüş yapılmaz.
- IScopedRepository<T>: tenant DB'sinde çalışır, ek olarak TenantId, şube erişimi ve IsDeleted filtrelerini zorunlu uygular.
- Şube seçimi olmayan tenant genelinde rapor bağlamı yalnız güvenilir servis tarafından kurulur. HasAllOrganizationAccess=false ve boş üyelik listesi şube erişimi vermez.
- Kapsam kaydı işlem sırasında değiştirilemez. Background job/webhook için ayrı DI scope açılıp sunucunun doğruladığı tenant bağlamı kurulmalıdır. Bu entegrasyonlar henüz yazılmadı.
- Normal tenant iş kodu IRepository<Employees> kullanamaz; IScopedRepository<Employees> kullanır. Repository ham Mongo koleksiyonunu dışarı açmaz.
- Mantıksal silme IsDeleted=true yapar. IsActive sorgudan otomatik gizlenmez; pasif kayıtların yönetimi için erişilebilir kalır, aktif liste isteyen servis açık filtre ekler.
- Güncelleme TenantId/OrganizationId/CreatedAt alanlarını değiştirmez. Upsert erişilemeyen veya silinmiş mevcut _id ile yeni kayıt açmaya çalışırsa Mongo tekillik kuralı reddeder; geri yükleme yapmaz.

## Register için sıra (servis ve form bağlantısı uygulandı)

1. Formdan işletme, kullanıcının adlandırdığı ilk şube, e-posta ve parola alınır. Rol, TenantId ve DatabaseName formdan atanmaz.
2. User ve Tenant kimlikleri önceden oluşturulur. Users.TenantId ve Tenants.OwnerUserId karşılıklı atanır.
3. DI üzerinden alınan TenantProvisioningService örneğinin PrepareNewTenant(tenant, branchName) metodu, sabit DB/ilk şube kimliğini ve ilk şube adını belirler.
4. Kullanıcı parolası hash'lenir. NormalizedEmail tekilliği merkezde korunur.
5. RegistrationRepository, kullanıcı + Pending tenant kaydını aynı Mongo session/transaction içinde yazar. Kullanıcı normal User rolünde ve PendingApproval durumundadır. NormalizedEmail unique index eşzamanlı kayıtları sınırlar. Mongo transaction desteği gerekir (Atlas/replica set); transaction yoksa kısmi kayıt yapan bir alternatif çalıştırılmaz.
6. TenantProvisioningService.ProvisionAsync(tenantId): süreli ve atomik alınan işlem sahipliğiyle işletme indekslerini ve ilk şubeyi hazırlar; yalnız başarı sonunda Active yapar.
7. Hata Failed durumuna geçirilir; DB veya kayıt silinmez. Süreç kapanırsa sahiplik süresi dolduktan sonra aynı DB/şube kimliğiyle tekrar denenebilir.
8. Tenant Active olsa bile kullanıcı PendingApproval kaldığı sürece erişim verilmez. Register otomatik cookie/JWT üretmez. Tenant hazırlığı hata verirse başvuru korunur ve sonuç IsTenantReady=false olur; superadmin onay akışında hazırlık tekrar denenmelidir. Yeniden deneme servisi rastgele tenant kimliğine açık anonim endpoint olmamalıdır.

## Login için sıra (01.10.2026 — servis ve web oturumu bağlı)

1. LoginRepository normalize e-postayla silinmemiş merkez Users kaydını bulur. LoginService BCrypt doğrulamasından sonra Active durumunu denetler; PendingApproval, Rejected ve kapalı hesaplara oturum vermez. Yanlış parola ile bulunamayan hesap aynı genel hatayı döndürür.
2. Tenant merkezi kayıttan kontrol edilir; Pending/Provisioning/Failed durumları iş ekranlarına açılmaz.
3. Başarılı girişte kullanıcı, tenant, doğrulanmış şube ve rol taşınır; DB ismi istemciden seçilmez. Web korumalı cookie kullanır; Beni hatırla kalıcılığı belirler. Servis ayrıca imzalı at+jwt/type=access token üretir; tarayıcıya ikinci JWT cookie yazılmaz. Mobil/API login ve refresh endpointleri henüz yoktur.
4. Authentication middleware çalıştıktan sonra TenantWorkContextMiddleware devreye girer.
5. TenantWorkContextResolver kullanıcı–tenant bağını, hesap/tenant durumunu ve şubeleri DB'den doğrular. Header/cookie ile seçilen yetkisiz şube reddedilir.
6. Erişilebilir aktif şube yoksa yeni login reddedilir. Mevcut oturumlarda şube, hesap ve tenant erişimi her korumalı istekte tekrar doğrulanır; rol değişmişse yeniden giriş gerekir. Anonymous login/register/logout sayfaları eski oturum nedeniyle engellenmez. Sonradan şube/kullanıcı eklemek mevcut tenant DB'sinde işlem yapar; yeni DB açmaz.
7. Platform admininin tenantsız yönetim oturumu ayrı yönetim authorization hattı olarak auth aşamasında tasarlanacak. Bu middleware normal tenant oturumları içindir, platform adminine kendiliğinden bütün DB'leri açmaz.

Program.cs sırası UseRouting → UseAuthentication → TenantWorkContextMiddleware → UseAuthorization şeklindedir. Uygulama kökü /account/login adresine yönlenir; Home/Index [Authorize] ile korunur. Login POST ve logout POST antiforgery korumalıdır. Yalnız yerel returnUrl kabul edilir. /Home ve /Home/Index ana ekrana gider; kök / adresini Account.Start açıkça login sayfasına yönlendirir. Cookie production ortamında Secure, geliştirmede isteğin HTTP/HTTPS düzenine uygundur.

## İndeksler ve işletim

- Uygulama başlangıcı yalnız merkez DB için ping ve hesap/tenant indekslerini hazırlar.
- İşletme indeksleri provisioning sırasında hazırlanır. Daha sonra aktif tenant DB'lerine yeni indeks sürümleri yayacak ayrı bakım işi gerekir; bütün tenant DB'leri her web açılışında taranmaz.
- İki aşamalı hazırlık bir Mongo çoklu-DB transaction'ı olarak sunulmaz; tekrar çalıştırılabilir provisioning uygulanır.
- Aynı cluster'da çok sayıda koleksiyon/indeksin kaynak maliyeti kapasite planına dahil edilmelidir.
- Mevcut tek-DB verileri otomatik taşınmaz. Önceden var olan Tenants kayıtları eşleme/hazırlık durumları tamamlanmadan aktif sayılmaz.
- Kullanıcı/çalışan isimleri gcm:v1 şifreleme formatındadır; eski plaintext/CBC verileri için ayrıca kontrollü migration gerekir.
- Gerçek Mongo/Atlas üzerinde tenant DB oluşturma, indeks çakışmaları, çökme/tekrar deneme ve eşzamanlılık kabul testi yapılmadan canlıya hazır sayılmaz.

## Doğrulama

tests/Randevona.Checks mevcut şifreleme kontrollerine ek olarak iki tenant'ın DB yönlendirmesini, şube sınırlarını, kapsam dışı yazmayı, soft delete/upsert davranışını, başlangıç hatalarını ve provisioning tekrarını bellek içi Mongo arayüzleriyle kontrol eder. Gerçek bağlantı veya DB yazımı yapılmaz.

RegisterService mevcut Result<RegisterResponse> yapısıyla formdan çağrılır. Servis doğrulama, parola hash, merkezde atomik kayıt ve tenant hazırlığını yönetir; controller yalnız form/sonuç yönlendirmesi yapar. LoginService ve oturum bağlantısı tamamlandı. Giriş/kayıt hız sınırı, parola değişiminde oturum iptali ve tenantsız superadmin yönetim girişi henüz tamamlanmadı. E-posta ve superadmin onay/red ekranı docs/TODO.md içinde ayrı tutulur.



## Ortam ve işletme adı — 02.10.2026

- MongoSettings:TenantDatabasePrefix zorunludur. appsettings.Development.json içinde randevona_dev, temel appsettings.json içinde randevona_prod tanımlıdır. Staging gibi ek ortamlar kendi önekini override etmelidir. Merkez MongoSettings.DatabaseName değiştirilmedi.
- Önek randevona_<ortam> biçiminde, küçük harf/rakam içeren en fazla 24 karakterdir. İşletme adı Türkçe karakterlerden sadeleştirilir, boşluk/noktalama alt çizgiye çevrilir. ASCII karşılığı olmayan adlar tenant olarak etiketlenir; tüm isimler merkezdeki unique DatabaseName indeksine tabidir.
- Mongo ad sınırı için toplam uzunluk en fazla 63 ASCII bayttır. Gerekirse işletme adı kısaltılır. Sonuna ID veya rastgele ek konulmaz; kısaltma sonucu çakışmalar da reddedilir.
- İşletme adını sonradan değiştirmek DB'yi taşımaz. Resolver kayıtlı adın biçimini, ortam önekini ve başka bir tenant kaydına atanmadığını doğrular. Başka tenant/ortam veya merkez DB'ye yönlendirme reddedilir.
- Daha önce oluşturulmuş randevona_t_<id> eşlemeleri yalnız kendi tam tenant kimliğiyle uyumluluk amacıyla kabul edilir. Bu eski adlarda ortam bilgisi yoktur; yeni ortam ayrımı eski DB'lere otomatik uygulanmaz. Eski veriyi yeni ada taşımak, koleksiyon/indeks/veri aktarımı ve eşleme güncellemesi içeren ayrı kontrollü migration gerektirir. Bu değişiklik Atlas'a veri yazmaz veya mevcut DB'leri yeniden adlandırmaz.
- 170 çevrimdışı kontrol geçti; adlandırma, isim çakışması, Türkçe karakterler, uzunluk sınırı, ortam/tenant reddi, eski eşleme ve register/login/provisioning davranışları test edildi. Solution build: 0 hata, 0 uyarı.

Kullanıcının netleştirdiği son biçim: Business name = Test → randevona_dev_test. Önceki ID ekli üretim kaldırıldı. Aynı DB adıyla iki kayıt yarışırsa merkezi unique indeks ve kayıt transaction'ı tek kazananı korur; kaybeden kullanıcı/tenant kaydı bırakılmaz. Servis Register.DatabaseNameExists koduyla İngilizce bir çakışma mesajı döndürür. Eski ID ekli mevcut DB'ler otomatik taşınmadı; bu değişiklik yeni kayıt üretimini düzeltir.

## SystemAdminSettings and startup initialization

SystemAdminSettings implements the existing ISettings interface. AddAppSettings discovers it by class/section name; no manual configuration binding is required. Existing settings keys are preserved: Emails (array), DefaultPassword, TenantName and OrganizationName. Optional FirstName/LastName default to System/Administrator.

AddIdentityServices registers SystemAdminSeeder and SystemAdminInitializerHostedService. Program registers AddMongoPersistence before AddIdentityServices, so Mongo connectivity and the unique central indexes are prepared before account seeding. If the section is absent or Emails is empty, seeding is skipped. Configured setup failures stop startup; MongoSettings.StartupTimeoutSeconds bounds the initialization. Concurrent host-start configuration has not been enabled.

For each distinct normalized email, the existing central Users record is checked, including deleted records. Existing passwords, roles, names and account states are never overwritten. Existing non-admin, inactive or deleted accounts are left unchanged and a warning is logged. New accounts are Active/SuperAdmin, receive a BCrypt password hash, tenant ID, first-branch membership and HasAllOrganizationAccess=true. No approval email is required for trusted startup configuration.

The first missing admin and its new tenant are inserted in the existing central transaction. TenantName supplies CompanyInfos.CompanyName, so the DB name remains randevona_dev_<company-name> without an ID suffix. OrganizationName supplies the first branch name. Additional configured admins share this system tenant. An existing customer tenant with the same DB name is rejected. A previous failed/pending preparation is retried for an existing active admin without recreating the account.

This keeps the current tenant-based login working for the bootstrap admin; it does not implement a tenantless management session or cross-customer management access. SystemAdminSettings is a deployment/startup setting, not a public registration request. Changing DefaultPassword later does not reset existing passwords. A new server pointing to the same DB reuses existing records; an empty DB is initialized when the app starts, provided the configured Mongo connection supports registration transactions.

Verification: solution build succeeded with 0 warnings and 0 errors; 191 offline checks passed. No real Atlas administrator was inserted during implementation.
