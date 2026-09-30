# Tenant başına veritabanı — karar ve uygulama notları

30.09.2026. Kullanıcının seçimi: merkez hesap veritabanı + işletme (tenant) başına ayrı veritabanı. Kullanıcı veya şube başına DB açılmaz. Bu değişiklik mevcut kayıtları taşımaz.

## Yerleşim

- MongoSettings.DatabaseName: merkez/kontrol veritabanının adı. Mevcut ayar anahtarı korunmuştur.
- Merkez koleksiyonları: Users ve Tenants. İleride platform abonelikleri ve WhatsApp numarası → tenant eşlemesi de bu sınırda tasarlanacak; henüz eklenmedi.
- İşletme koleksiyonları: Organizations, Employees; ileride randevu, contact, mesaj ve diğer iş verileri.
- Tenant DB adı sunucu tarafından tenant ObjectId'sinden randevona_t_<id> olarak üretilir ve Tenants.DatabaseName üzerinde tutulur. İstekten DB adı alınmaz.
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
3. TenantProvisioningService.PrepareNewTenant(tenant, branchName), sabit DB/ilk şube kimliğini ve ilk şube adını belirler.
4. Kullanıcı parolası hash'lenir. NormalizedEmail tekilliği merkezde korunur.
5. RegistrationRepository, kullanıcı + Pending tenant kaydını aynı Mongo session/transaction içinde yazar. Kullanıcı normal User rolünde ve PendingApproval durumundadır. NormalizedEmail unique index eşzamanlı kayıtları sınırlar. Mongo transaction desteği gerekir (Atlas/replica set); transaction yoksa kısmi kayıt yapan bir alternatif çalıştırılmaz.
6. TenantProvisioningService.ProvisionAsync(tenantId): süreli ve atomik alınan işlem sahipliğiyle işletme indekslerini ve ilk şubeyi hazırlar; yalnız başarı sonunda Active yapar.
7. Hata Failed durumuna geçirilir; DB veya kayıt silinmez. Süreç kapanırsa sahiplik süresi dolduktan sonra aynı DB/şube kimliğiyle tekrar denenebilir.
8. Tenant Active olsa bile kullanıcı PendingApproval kaldığı sürece erişim verilmez. Register otomatik cookie/JWT üretmez. Tenant hazırlığı hata verirse başvuru korunur ve sonuç IsTenantReady=false olur; superadmin onay akışında hazırlık tekrar denenmelidir. Yeniden deneme servisi rastgele tenant kimliğine açık anonim endpoint olmamalıdır.

## Login için sıra (henüz cookie/JWT giriş servisi uygulanmadı)

1. Hesap merkez Users kaydından bulunur, parola hash doğrulaması ve hesap durumu kontrol edilir.
2. Tenant merkezi kayıttan kontrol edilir; Pending/Provisioning/Failed durumları iş ekranlarına açılmaz.
3. Oturumda kullanıcı ve tenant kimliği taşınır; DB ismi istemciden seçilmez.
4. Authentication middleware çalıştıktan sonra TenantWorkContextMiddleware devreye girer.
5. TenantWorkContextResolver kullanıcı–tenant bağını, hesap/tenant durumunu ve şubeleri DB'den doğrular. Header/cookie ile seçilen yetkisiz şube reddedilir.
6. Kullanıcı şubesiz kaldıysa ona şube erişimi verilmez. Sonradan şube/kullanıcı eklemek mevcut tenant DB'sinde işlem yapar; yeni DB açmaz.
7. Platform admininin tenantsız yönetim oturumu ayrı yönetim authorization hattı olarak auth aşamasında tasarlanacak. Bu middleware normal tenant oturumları içindir, platform adminine kendiliğinden bütün DB'leri açmaz.

Program.cs sırası UseRouting → UseAuthentication → TenantWorkContextMiddleware → UseAuthorization şeklindedir. Uygulama kökü /account/login adresine yönlenir; Home/Index [Authorize] ile korunur. LoginService henüz uygulanmadı.

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

RegisterService mevcut Result<RegisterResponse> yapısıyla formdan çağrılır. Servis doğrulama, parola hash, merkezde atomik kayıt ve tenant hazırlığını yönetir; controller yalnız form/sonuç yönlendirmesi yapar. Sıradaki geliştirme LoginService ve oturum bağlantısıdır. E-posta ve superadmin onay/red ekranı docs/TODO.md içinde ayrı tutulur.

