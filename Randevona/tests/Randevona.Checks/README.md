# Mongo ve şifreleme kontrolleri

> This is a separate test console project. Its Program.cs and MemoryMongo are not part of the Web startup path. The Web project has no reference to this project. Running or publishing Randevona.csproj does not execute these checks. MemoryMongo is a test double, not a production database, cache or database backup. Real MongoDB integration tests remain necessary.
Solution klasöründe:

```text
dotnet run --project tests/Randevona.Checks/Randevona.Checks.csproj
dotnet run --project tests/Randevona.Checks/Randevona.Checks.csproj -- --late-map
```

İlk komut 170 çevrimdışı kontrol, ikinci komut ayrı süreçte geç şifreleme eşlemesi kontrolü çalıştırır. Gerçek veritabanı bağlantısı açılmaz. Mongo arayüzleri bellek içi test nesneleriyle değiştirilir. Gerçek Mongo indeks davranışı ve Atlas erişimi bu testin kapsamı dışındadır.

## Uygulama davranışı

- Program önce AddAppSettings(configuration), sonra AddMongoPersistence() çağırır.
- MongoSettings yalnız AddAppSettings tarafından kaydedilir.
- MongoClient ve IControlMongoDbContext singleton, tenant IMongoDbContext scoped olarak kullanılır. Ortak bir IMongoDatabase servisi yoktur.
- Açılış servisi merkez DB için ping ve gerekli indeksleri tamamlar. Tenant DB indeksleri provisioning sırasında hazırlanır. Hata gizlenmez.
- MongoSettings:StartupTimeoutSeconds varsayılan 30, geçerli aralık 1–120 saniyedir.
- Users.NormalizedEmail için genel tekil indeks bulunur. Normalize edilmiş aynı e-postanın mevcut birden fazla kaydı varsa indeks oluşturma başarısız olur; mevcut veriler otomatik silinmez veya birleştirilmez.
- Users, Tenants, Organizations ve Employees için sorgu indeksleri hazırlanır. İndeksler tek başına tenant yetkisi uygulamaz; repository yetkilendirmesi sonraki aşamadır.

## Şifreleme ve veri uyumluluğu

- Users ve Employees içindeki FirstName/LastName alanları EncryptedField ile işaretlidir.
- Yeni değerler rastgele nonce kullanan AES-GCM ile gcm:v1: formatında saklanır.
- Şifreleme anahtarı rastgele üretilmiş, en az 32 UTF-8 baytlık bir sır olmalıdır. Uzunluk denetimi tek başına anahtarın rastgeleliğini garanti etmez.
- Null ve boş metin ayrı korunur. Yanlış anahtar, bozuk veri ve desteklenmeyen format açık hata verir.
- Eski CBC veya düz metin kayıtları otomatik kabul edilmez. Var olan veriler için yedekli, kontrollü bir migration gerekir; bu çalışma böyle bir migration çalıştırmaz.
- Anahtarı değiştirmek eski kayıtları yeniden şifrelemez. Anahtar değişimi ayrıca planlanmalıdır.
- Şifrelenen isimler üzerinde mevcut düz metin eşitlik/regex sorguları çalışmaz; arama ihtiyacı ayrıca tasarlanmalıdır.
- Email/NormalizedEmail ve kimlik alanları şifrelenmez. PasswordHash parola hash'i olarak kalır. Tüm kişisel verilerin şifrelendiği veya KVKK/Meta uyumunun tamamlandığı anlamına gelmez; veri saklama ve erişim politikası TODO'da kalır.
- Gerçek Development ayarları testlerde kullanılmaz, sırlar test koduna veya rapora kopyalanmaz.

Tenant DB seçimi, şube sınırları, scoped yazma ve provisioning tekrarları da bellek içi Mongo arayüzleriyle kontrol edilir. Gerçek sunucu davranışı ayrıca test edilmelidir. Mimari akış docs/Tenant-veritabani-mimarisi.md içindedir.

## Password servisi

- ForgotPassword, ConfirmVerificationCode ve ResetPassword mevcut Domain Result<T> modelini döndürür. Hata bilgisi result.Error içindedir.
- Şifre hash işlemleri BCrypt.Net-Next kullanır. 12 karakter alt sınırı yoktur; boş parola reddedilir. BCrypt sınırı nedeniyle en fazla 72 UTF-8 bayt kabul edilir (karakter sayısı değildir).
- E-posta araması Trim().ToUpperInvariant() ile NormalizedEmail üzerinden yapılır. Register servisi de aynı normalizasyonu uygulamalıdır.
- Sıfırlama isteği 5 dakika geçerlidir. İstek başına 5 yanlış kod denemesi sonrasında yeni istek gerekir. Doğrulanmamış veya tüketilmiş token şifre değiştiremez.
- Şifre değiştirme ve token tüketme tek koşullu Mongo güncellemesidir. Eşzamanlılık kontrolü, iki isteğin aynı geçerli kaydı okuduğu durumla sınanır; bellek içi test gerçek Mongo sunucu testinin yerine geçmez.
- Mail gönderim bloğu şimdilik yorumdadır. ForgotPassword yalnızca merkezi Users kaydında sıfırlama isteği oluşturur ve reset token döndürür. Doğrulama kodu yanıt veya log içine yazılmaz; testler kodu bellek içi test kaydından okur.
- Mail servisi tamamlanana kadar kullanıcıya kod teslimi ve uçtan uca şifremi unuttum akışı hazır değildir. Kod doğrulama, süre, deneme sınırı ve tek kullanımlık token kontrolleri korunur. Mail testleri yeniden etkinleştirme aşamasında geri eklenecek.
- Web IdentityService projesine referans verir; AddIdentityServices password/register/login servislerini scoped kaydeder. PasswordService JwtSettings, IRepository<Users> ve ILogger<PasswordService> alır; mail servisi almaz. Register ve login formları bağlı; password-reset endpoint bağlantıları sonraki adımdır.
- Endpoint eklenirken sıfırlama isteği hız sınırı, kullanıcı varlığını açığa çıkarmayan yanıtlar ve şifre değişiminden sonra mevcut oturumların iptali ele alınmalıdır.



## Register kontrolleri

RegisterChecks gerçek RegisterService, RegistrationRepository ve TenantProvisioningService sınıflarını bellek içi Mongo arayüzleriyle çalıştırır. Doğrulama, normalizasyon, password hashing, tenant/şube ilişkileri, onay bekleyen kullanıcının erişim reddi, aynı e-posta yarışı, iki insert'in aynı session kullanması, ikinci insert hatasında rollback, transaction yokken hata verme ve provisioning tekrarını kontrol eder.

MemoryMongo transaction davranışını yalnız test amacıyla taklit eder; gerçek sunucunun transaction retry/commit, indeks ve eşzamanlılık davranışını doğrulamaz. Atlas/replica-set üzerinde entegrasyon testi ayrıca yapılmalıdır. Kayıt transaction gerektirir; standalone Mongo için atomikliği bozan bir fallback yoktur.

HTTP sınırı ayrıca geçici, DB bağlantısız host ve sahte kayıt servisiyle kontrol edildi: kök adres login yönlendirmesi, Home için auth challenge, register POST sonrası onay bekleme sayfası, oturum açılmaması ve CSRF reddi. Bu kontrol gerçek Atlas'a kayıt yapıldığı anlamına gelmez.

## Login ve oturum kontrolleri — 01.10.2026

LoginChecks, gerçek LoginService/LoginRepository ve tenant resolver ile 25 kontrol ekler: onay/hesap durumları, normalize e-posta, yanlış parola, silinmiş hesap, kapalı veya hazırlanmamış tenant, hatalı DB eşlemesi, erişilebilir şube, imzalı JWT kapsamı/süresi/audience, benzersiz jti, cancellation ve mevcut oturumun durum/rol değişince reddi.

Ayrı geçici HTTP hostunda mevcut Program.cs auth/routing ayarları, gerçek MVC controller/servis/repository ve bellek içi Mongo ile 15 kontrol geçti: kök → login, anonim Home reddi, login/logout CSRF, onay bekleme mesajı, başarısız parolanın HTML'e yazılmaması, harici returnUrl reddi, cookie ile Home erişimi, Beni hatırla, logout ve logout sonrası erişim reddi. Geçici host test ayarları ve geçici Data Protection anahtarları kullanır; production başlatma veya gerçek Atlas erişimi testi değildir.

Kalan işler: giriş/kayıt hız sınırı, parola değişimi sonrası tüm oturumların iptali, tenantsız superadmin yönetim oturumu, gerçek Atlas entegrasyonu. Testler bu eksiklerin tamamlandığı anlamına gelmez.

TenantNamingChecks ortam/ana şirket adı adlandırması için 17 kontrol ekler. Test ayarları TenantDatabasePrefix içerir; gerçek Atlas bağlantısı kullanılmaz. Mevcut tenant/register/login kontrolleri yeni adlandırma üzerinden çalışır.



RegisterChecks artık Business name kaynaklı IDsiz DB adını, aynı normalize adın reddini ve eşzamanlı isim çakışmasında rollback davranışını da doğrular. MemoryMongo test nesnesi merkez DatabaseName unique indeksini taklit eder; gerçek Mongo entegrasyon testi ayrıca gereklidir.

