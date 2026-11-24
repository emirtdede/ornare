# Rakip Karşılaştırması - Acımasız Puanlama (24 Kasım 2026)

Bu belge, Ornare'nin Windows 11'de klasör simgesi kişiselleştirme pazarındaki gerçek yerini
ölçmek için yazıldı. Amaç moral vermek değil, ürünü nerede kaybettiğini göstermektir. Her puan
bir kanıta dayanır; kanıtı olmayan hiçbir puan verilmedi.

Varsayımlar:

- Ölçülen iş: "Windows Gezgini'nde bir klasörün simgesini kalıcı, güvenli ve geri alınabilir
  biçimde değiştirmek".
- Puanlar 0-100 arasıdır ve ağırlıklıdır. Toplam 100 tam puan eder.
- Puanlama tarihi 24 Kasım 2026. Fiyatlar ve sürümler bu tarihte yayınlanmış bilgilerdir.
- Bir ürün "başka bir işi iyi yapıyor" ise bu, bu tabloda puan kazandırmaz; yalnızca bu işi
  yapıp yapmadığı ölçülür.
- Kanıt seviyesi ürün başına farklıdır ve bu fark puanı etkiler. Ornare'nin puanı kendi
  kaynağından ve test koşusundan gelir (35.858 satır ürün kodu, 15.366 satır test, 637 davranış
  testi, 21 performans kapısı). Rakiplerin puanı yayınlanmış belge, sürüm notu, fiyat sayfası ve
  bağımsız incelemeden gelir; hiçbir rakibin kaynağı okunmadı, bu yüzden bir rakibin
  "belgelenmemiş" sayılan özelliği gerçekte var olabilir. Bu belirsizlik rakibin lehine değil
  aleyhine yorumlandı: kanıtlanmayan garanti, garanti sayılmadı.

Bu turda tabloya üç ürün eklendi, çünkü 2026 pazarında klasör simgesi işini yapan asıl rekabet
artık Microsoft Store tarafında: Folderly, IconStyler ve Fold11. Üçü de eski masaüstü
araçlarından daha modern arayüze ve daha düşük fiyata sahip.

## 1. Ağırlıklar

| # | Kriter | Ağırlık | Neden bu ağırlık |
|---|--------|---------|------------------|
| A | Simge kalitesi ve render (çok boyutlu ICO, HiDPI, rozet, emoji) | 14 | Kullanıcının gördüğü tek somut çıktı |
| B | Windows 11 uyumu ve güvenilirlik (desktop.ini doğruluğu, atomik yazma, Explorer tazeleme, Haziran 2026 sertleştirmesi) | 18 | Ürün burada sessizce bozulursa diğer her şey anlamsız |
| C | Veri bütünlüğü ve geri alınabilirlik (öncesi durumu koruma, Undo, Reset, çakışma tespiti) | 14 | Kullanıcının mevcut özelleştirmesini ezmek kabul edilemez |
| D | Arayüz olgunluğu (modern UI, karanlık/açık tema, klavye, ekran okuyucu, çok dilli) | 14 | Günlük kullanım kalitesi |
| E | Toplu işlem ve ölçek (çoklu seçim, kütüphane, arama, son işlemi uygula) | 10 | 20 klasörü tek tek yapan ürün terk edilir |
| F | Performans ve kurulum ayak izi | 10 | 36 MB'lik bir araç, 3 MB'lik bir araçla aynı işi yapıyorsa kaybeder |
| G | Gizlilik ve çevrimdışı çalışma (hesap, bulut, telemetri zorunluluğu) | 6 | Klasör adları kişisel veridir |
| H | Fiyat / değer (tek seferlik mi, abonelik mi, ücretsiz katman) | 7 | Abonelik, bu iş için savunulamaz |
| I | Sürdürülebilirlik ve mimari (test, katmanlar, açık kaynak, API/CLI) | 7 | Ürünün 3 yıl sonra hala çalışıp çalışmayacağı |

## 2. Ana tablo

| Kriter (ağırlık) | Ornare | Folder Marker | Folderly | Files App | IconStyler | Fold11 | FolderIco | WinSetView | Folder Painter | IconPackager | CustomizerGod |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A. Simge kalitesi (14) | 11 | 12 | 8 | 7 | 10 | 11 | 11 | 1 | 6 | 8 | 5 |
| B. Win11 uyumu ve güvenilirlik (18) | 16 | 12 | 9 | 6 | 8 | 8 | 11 | 9 | 10 | 7 | 3 |
| C. Veri bütünlüğü (14) | 13 | 6 | 10 | 8 | 7 | 5 | 6 | 6 | 4 | 5 | 3 |
| D. Arayüz olgunluğu (14) | 8 | 8 | 8 | 12 | 8 | 9 | 9 | 5 | 5 | 8 | 3 |
| E. Toplu işlem (10) | 8 | 9 | 6 | 10 | 7 | 8 | 8 | 7 | 2 | 7 | 5 |
| F. Performans / ayak izi (10) | 8 | 6 | 9 | 4 | 8 | 9 | 7 | 9 | 9 | 7 | 8 |
| G. Gizlilik / çevrimdışı (6) | 6 | 5 | 6 | 5 | 5 | 2 | 3 | 6 | 6 | 3 | 5 |
| H. Fiyat / değer (7) | 6 | 6 | 7 | 4 | 7 | 7 | 3 | 7 | 7 | 5 | 7 |
| I. Mimari / sürdürülebilirlik (7) | 6 | 3 | 3 | 7 | 3 | 3 | 3 | 6 | 4 | 3 | 2 |
| **TOPLAM (100)** | **82** | **67** | **66** | **63** | **63** | **62** | **61** | **56** | **53** | **53** | **41** |

Okunuşu: Ornare hala yalnızca iki kriterde birinci (veri bütünlüğü 13/14, Win11 güvenilirliği
16/18) ve bu turda mimari puanı 5'ten 6'ya çıktı çünkü iddia ile kod arasındaki üç çelişki
kapandı. Arayüz olgunluğunda (Files App 12, Fold11 9) ve simge çeşitliliğinde (Folder Marker 12)
kaybediyor. 82 puan "sevke hazır" demek değildir; 5. bölüm bunu ayrıca ölçer.

## 3. Ürün bazlı acımasız notlar

### Ornare - 82/100 (mimari ve veri bütünlüğü 1., sevke hazırlık: HAYIR)

Kazandığı yerler:

- desktop.ini yazımı tek bir huniden geçiyor: geçici dosya, diske flush, `MoveFileExW` ile
  write-through atomik rename. Altındaki tek Win32 commit noktası `AtomicFileWriter`; hem
  desktop.ini hem ICO/PNG dışa aktarma aynı yolu kullanıyor. Rakiplerin hiçbirinde bunun kanıtı yok.
- Orijinal kodlama korunuyor (UTF-16 LE/BE, UTF-8 +/- BOM, eski ANSI). Türkçe klasör adı olan
  bir ANSI desktop.ini, rakip araçların çoğunda bozulur.
- Haziran 2026 güvenlik güncellemesinin (KB5094126) yol açtığı "desktop.ini yok sayılıyor"
  arızasını tespit eden ve onaran tek ürün. Bu, tablodaki en somut teknik ayrıştırıcı. Artık
  yalnızca belgede değil kodda: `ShellTrustGuard` Zone.Identifier akışını temizliyor,
  `GetFolderState` temizlenemeyen klasörü `ConfigMismatch` bildiriyor.
- 250 karakterlik bir klasörde 262 karakterlik desktop.ini yolu artık `ERROR_PATH_NOT_FOUND`
  vermiyor; alt yollar `LongPath.ToExtended` ile `\\?\` öneki alıyor.
- Baseline, jurnal, uzlaştırma (reconciliation) ve Undo var; "Reset" yalnızca Ornare'nin
  sahiplendiği simge anlamlarını geri alıyor, yabancı içeriği ezmiyor.
- 637 davranış testi, 21 performans kapısı, 6 dil, çevrimdışı, hesapsız. Boş `catch` bloğu sıfır;
  64 `async void` giriş noktasının tamamı `GuardAsync` üzerinden hata yüzeyine bağlı (63 çağrı).

Kaybettiği yerler (puanı düşüren gerçekler):

- Arayüz 8/14. Titreme (flash) yok: pencereler `InitializeComponent` sonrası, `Activate`
  öncesi yerelleştirmeyi uyguluyor. Erişilebilirlik adları artık altı dilde yerelleşiyor: test
  özellik bazında zorluyor ve bu sayede altı öğenin İngilizce kalan ekran okuyucu adı bulundu ve
  düzeltildi. Kaybedilen puan bakım maliyeti: XAML'de 0 adet `x:Uid` var ve 120 görünür metin XAML
  içinde İngilizce gömülü; her biri kodda elle eşleniyor. Yeni bir metin eklemek sözlükte ve kod
  eşlemesinde değişiklik ve parity testinin geçmesi demek. Daha önce iki paralel yerelleştirme
  deposu vardı: kod sözlükleri ve `Strings/*/Resources.resw`. İlk çalıştırma denetiminde aynanın
  436 anahtarın yalnızca 259'unu taşıdığı, hiçbir derlemede `resources.pri` üretmediği ve
  testlerin onu yalnızca kendisiyle karşılaştırdığı görüldü; bu yüzden silindi (denetim bulgusu
  3.4, ADR-0014 revizyonu). Kalan maliyet yalnızca XAML literallerinin kodda eşlenmesi.
- 259 karakter sınırı bilinçli bir karar (ADR-0011), kusur değil; ama Folder Marker ve Folderly
  bu sınırı hiç konuşmuyor bile, o yüzden kullanıcı gözünde bir kısıt olarak kalıyor.
- `QuickPanelWindow` 3.238 satırdan 2.546 satıra indi (yerleşim, geçici dosyalar, kataloglar ve
  dil yüzeyi ayrı dosyalarda); `ManagerWindow.xaml.cs` 2.880 satır. Kod-behind ayrıştırması
  sürüyor, bakımı hala pahalı.
- Ürün imzasız ve yüklenebilir MSIX ile doğrulanmadan sevk edilemez; "kullanıcıya ulaştı" kanıtı
  yok. Bu yüzden H (fiyat/değer) 6/7: bedava olması iyi, ama kanıtlanmış kurulum yok.
- Simge kataloğu ticari paketler kadar geniş değil; rozet taksonomisi (durum/öncelik) rakipten
  alınmalı. Fold11 tek başına 1.400'den fazla hazır simge sunuyor; Ornare'nin vektör kütüphanesi
  bu ölçekte değil.

### Folder Marker - 67/100 (bugün bir kullanıcının alacağı ürün)

- Güçlü: durum ve öncelik rozetleri (Folder Marker'ın en iyi fikri), çoklu seçim, olgun kod
  tabanı, tek seferlik fiyat (Pro 24,95-34,95 USD), 36,8 MB, v4.9.1.
- Zayıf: gerçek Undo yok, yalnızca "varsayılana döndür". Önceki desktop.ini içeriğini koruma
  garantisi belgelenmemiş. Haziran 2026 sertleştirmesine dair hiçbir işlem yok.
- 36,8 MB, tek iş için ağır. Arayüz Win32 klasik; karanlık tema tutarlı değil.
- Simge kalitesi 12/14: rozet fikri doğru, çizimler eski.

### FolderIco - 61/100

- Güçlü: emoji klasörler ve binlerce hazır simge; arayüz Folder Marker'dan modern.
- Zayıf: abonelik (19,95 USD'den 29,99 USD/yıl). Klasör simgesi için abonelik savunulamaz.
  Hesap ve aktivasyon çevrimiçi; G 3/6.
- Veri bütünlüğü 6/14: çakışma tespiti ve önceki durumu koruma belgelenmemiş.

### Sordum Folder Painter - 53/100

- Güçlü: tek dosya, taşınabilir, bedava, çevrimdışı, 3 MB civarı. "Basit işi basit yap" dersi.
- Zayıf: yalnızca düz renk; özel .ico yok (6/14). Toplu işlem yok (2/10). Undo yok.
- Ornare'nin alması gereken fikir: taşınabilir mod ve "hiçbir şey kurmadan dene" vaadi.

### Stardock IconPackager - 53/100

- Farklı iş: sistem geneli simge temaları. Klasör başına desktop.ini yazmıyor, kabuk simge
  önbelleğini değiştiriyor.
- Zayıf: güncelleme sonrası kırılma riski, yönetici gereksinimi, aktivasyon. B 7/18.
- Bu ürün "tema" satıyor, "kalıcı klasör kimliği" satmıyor; Ornare ile aynı kategoride değil.

### Files App - 63/100

- Güçlü: en iyi arayüz (12/14), gerçek etiket (tag) veritabanı, erişilebilirlik, açık kaynak,
  toplu işlem (10/10).
- Zayıf: Explorer'ın yerine geçmiyor. Etiketleri yalnızca kendi penceresinde görünür; Windows
  Gezgini'nde klasör simgesi değişmez. Bu yüzden B 6/18: ölçtüğümüz işi yapmıyor.
- Ornare'nin alması gereken fikir: kaynak/`x:Uid` disiplini, erişilebilirlik ve etiket modeli.

### Fold11 - 62/100 (en geniş katalog, en zayıf güven)

- Güçlü: 1.400'den fazla hazır simge, topluluk kütüphanesi, yerleşik simge üreticisi (renk,
  degrade, metin, PNG/JPG/SVG/ICO yükleme), çoklu klasöre uygulama, sağ tık "Edit with Fold11",
  Microsoft Store üzerinden ücretsiz.
- Zayıf: veri bütünlüğü 5/14. Undo, baseline ve önceki desktop.ini koruma garantisi yok.
  Topluluk yüklenen simgeler bir güven sınırı: kaynağı bilinmeyen ICO/SVG indirilip klasöre
  gömülüyor, bu Ornare'nin SVG güvenlik doğrulamasının tam tersi bir model. Katalog çevrimiçi,
  bu yüzden G 2/6: çevrimdışı vaadi yok ve indirilen simge doğrulanmıyor.
- Ornare'nin alması gereken fikir: katalog ölçeği ve "kendi simgeni üret" akışı. Ornare'nin
  şablon/amblem kütüphanesi teknik olarak daha güvenli, ama kullanıcıya daha az şey gösteriyor.

### IconStyler - 63/100 (en geniş kapsam, en riskli kapsam)

- Güçlü: yalnızca klasör değil sürücü, program, dosya türü simgeleri de değiştiriyor. "Icon
  Guard" güncelleme sonrası simgeyi korumayı vaat ediyor (Haziran 2026 sorununa doğru refleks,
  ama mekanizması belgelenmemiş). "Live Folders" ve tek seferlik 19,99 USD ömür boyu lisans.
- Zayıf: B 8/18. Sürücü ve uygulama simgelerine dokunmak, kayıt defteri ve kabuk önbelleği
  üzerinden çalışmayı gerektirir; bu, Haziran 2026 sonrası en kırılgan yüzey. Atomik yazma,
  kodlama koruma ve MOTW temizliği kanıtı yok. C 7/14.
- Not: "Icon Guard" fikri Ornare'ye alınmaya değer, ama Ornare'nin bunu yapma biçimi farklı
  olmalı: sisteme yamalamak değil, sahiplendiği dosyayı yeniden doğrulayıp onarmak.

### Folderly - 66/100 (en yakın rakip, en dürüst kapsam)

- Güçlü: gerçek geri alma geçmişi var ve "tek tek veya tümünü geri al" sunuyor; uygulama
  sonrası otomatik tazeleme; ICO dönüştürme derdi yok; Microsoft Store, yerel çalışıyor,
  deneme sürümü ve 1,49 USD tanıtım fiyatı. C 10/14 ile Ornare'den sonra en iyi ikinci.
- Zayıf: derinlik yok. Toplu işlem 6/10: geçmiş listesi var, şablon/kütüphane yok. Atomik yazma,
  kodlama koruma ve MOTW temizliği belgelenmemiş; bu yüzden B 9/18'de kalıyor, çünkü Haziran
  2026 arızasını ne tespit ettiğine ne onardığına dair kanıt var. Genç ürün, kurulum ömrü
  kanıtlanmamış.
- Bu ürün, Ornare'nin pazarlama cümlesini çürüten en net örnek: "geri alınabilirlik" tek başına
  ayrıştırıcı değil, çünkü 1,49 USD'lik bir Store uygulaması da sunuyor. Ornare'nin
  ayrıştırıcısı geri alma değil, doğrulama ve onarım.

### CustomizerGod - 41/100

- `imageres.dll` gibi sistem dosyalarını yamalıyor. Yönetici ister, her Windows güncellemesinde
  kırılır, geri alma kullanıcının sorumluluğunda. B 3/18, C 3/14.
- Bu yaklaşım Ornare'nin temel kuralıyla (sistem dosyalarını değiştirme) doğrudan çelişir;
  karşılaştırmaya "yapılmaması gereken" örneği olarak girdi.

### WinSetView - 56/100

- Farklı iş: klasör görünüm ayarları (sütunlar, sıralama). Simge değiştirmiyor (A 1/14).
- Alınması gereken fikir: her işlemden sonra "ne değişti" raporu ve toplu betik dostu çıktı.
  Ornare'nin CLI zarfı bunun için doğru temel.

## 4. Hangi proje seçilir?

İki ayrı soru var ve dürüst cevap iki farklı ürünü işaret ediyor:

**Bugün, hemen bir klasör simgesi değiştirmek isteyen kullanıcı olsam:** Folder Marker Pro.
Tek seferlik ödeme, rozet taksonomisi, olgun kurulum. Kusurlu ama çalıştığı kanıtlanmış.

**Hemen, en az parayla ve kurulum derdi olmadan:** Folderly. 66 puanla Folder Marker'ın bir
puan altında, çünkü gerçek geri alma geçmişi var ve 1,49 USD. Katalog derinliği ve kurulum ömrü
kanıtı yok; bu yüzden birinci değil. Ekosistemi daha geniş tutmak isteyen IconStyler'a bakabilir,
ama sürücü ve uygulama simgelerine dokunan her araç Haziran 2026 sonrası daha kırılgandır.

**Uzun vadeli bir ürün inşa edecek olsam ve kaynağım bu olsa:** Ornare. Tek başına iki şey
karar veriyor:

1. Haziran 2026 güncellemesi rakiplerin tamamını sessizce bozdu; Ornare bunu tespit edip
   onarabilen tek ürün. Bu bir reklam cümlesi değil, kodda `ShellTrustGuard` ve
   `FolderCustomizationState.ConfigMismatch` olarak duruyor, ve bu turda üç mimari test onun
   sessizce kaldırılmasını engelliyor.
2. Atomik, kodlama korumalı, önceki durumu ezmeyen yazma. Rakiplerin hiçbiri bunu garanti
   etmiyor; kullanıcı verisi söz konusu olduğunda bu, arayüz güzelliğinden daha ağır basar.

**Ama Ornare bugün olduğu gibi genel dağıtıma çıkamaz.** Aşağıdaki karnede kapanmamış iki madde
var ve ikisi de kodla değil, altyapıyla ilgili. "Hangi proje" sorusunun cevabı bu yüzden hala
Folder Marker; ama fark kapanıyor, açılmıyor. Ornare 79'dan 82'ye çıktı; Folder Marker 67'de
sabit kaldı ve arkasından Folderly 66 ile yaklaştı. Yani liderlik korunuyor, güvenlik marjı
kalmıyor.

## 5. Ornare sevke hazırlık karnesi

Kapanan maddeler (bu turda):

| # | Bulgu | Kapanış |
|---|-------|---------|
| 1 | Uzun yol belgesi kodla çelişiyordu | Belge koddan yazıldı, karar ADR-0011'e bağlandı, sınır testlerle sabitlendi |
| 2 | Haziran 2026 sertleştirmesi sessiz başarısızlık üretiyordu | `ShellTrustGuard` Mark of the Web'i temizliyor, `ConfigMismatch` durumu Onarım'a yönlendiriyor |
| 3 | desktop.ini yazımı iki ayrı yerde kopyalanmıştı | Tek huni: `DesktopIniFileWriter`, altında tek commit noktası `AtomicFileWriter`; 3 mimari test geri gelmesini engelliyor |
| 4 | Shell eklentisinde 5 kopya `Invoke`, 8 kopya `ShellExecuteExW` | `OrnareCommandBase`: 1.632 satırdan 1.131 satıra, 1 `Invoke`, 2 `ShellExecuteExW` |
| 5 | `SqliteStorageService` tek dosyada 3.795 satırdı | Şema, V2-V6 göçleri ve bağlantı açma `SqliteStorageService.Schema.cs` dosyasına ayrıldı |
| 6 | `docs/WORK_REPORT.md` `QA_REPORT.md` ile çelişiyordu | Silindi; altı benzersiz karar ADR-0012'ye taşındı |
| 7 | `ARCHITECTURE.md` kurulmamış projeleri anlatıyordu | Kurulu mimariyi anlatacak şekilde yeniden yazıldı |
| 8 | Altı öğenin ekran okuyucu adı altı dilde İngilizce kalıyordu | Test özellik bazında zorluyor; adlar `Settings_AccentLabel`, `Settings_ScaleLabel`, `Onboarding_CloudToggle`, `Action_RemoveEmblem` anahtarlarından geliyor |
| 9 | `QuickPanelWindow` 3.238 satırlık tek dosyaydı | Yerleşim, geçici dosyalar, kataloglar ve dil yüzeyi ayrı partial dosyalara taşındı; sınıf 2.546 satır |
| 10 | Denetim raporu `QA_REPORT.md` ile ikinci bir doğruluk kaynağı oluşturuyordu | `PRE_DEPLOY_AUDIT_2026-11-22.md` silindi; her bulgu sınıfının yeni adresi ADR-0013'te kayıtlı |

Açık maddeler:

| # | Blokaj | Kanıt | Kapanış koşulu |
|---|--------|-------|----------------|
| 1 | Sertifika geliştirme sertifikası | `docs/QA_REPORT.md` 7.4 | Güvenilir kod imzalama sertifikası + Store kimliği |
| 2 | ARM64 ve 26H1 nitelendirilmedi | `docs/QA_REPORT.md` 7.2, 7.3 | Gerçek donanımda nitelendirme; sahte hedef üretilmeyecek |

Puan kaybı olan, blokaj olmayan maddeler:

| # | Borç | Ölçü |
|---|------|------|
| 3 | Yerelleştirme iki yerde tutuluyor | 0 `x:Uid`, 120 gömülü XAML metni, 2 paralel kaynak deposu |
| 4 | `ManagerWindow` hala büyük | 2.880 satır; dil ve ayarlar yüzeyleri ayrıldı, geri kalanı açık |
| 5 | Rozet taksonomisi yok | Folder Marker'ın durum/öncelik fikri, çizimler bize ait |
| 6 | Simge kataloğu ölçeği | Fold11 1.400'den fazla hazır simge sunuyor, Ornare daha az |
| 7 | `WaitAsync(cancellationToken)` yayılımı | İmza değişikliği tüm çağrı grafiğini etkiliyor, ayrı iş |

## 6. Kaynaklar

- Microsoft, "How to Customize Folders with Desktop.ini":
  https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini
- Microsoft, "June 9, 2026 - KB5094126 (OS Builds 26200.8655 and 26100.8655)":
  https://support.microsoft.com/en-us/servicing/os/windows-11/2026/06/june-9-2026-kb5094126-os-builds-26200-8655-and-26100-8655
- Windows Central, "It is intentional: Microsoft says Windows 11's broken folder icons are by
  design" (Mark of the Web istisnası ve elle kaldırma yolu):
  https://windowscentral.com/microsoft/windows-11/it-is-intentional-microsoft-says-windows-11s-broken-folder-icons-are-by-design-heres-whats-going-on
- WindowsForum, "KB5094126 Windows 11 June 2026: Desktop.ini Icons and Localized Names Disappear":
  https://windowsforum.com/threads/kb5094126-windows-11-june-2026-desktop-ini-icons-localized-names-disappear.425845/post-986482
- Folder Marker (sürüm 4.9.1, 36,8 MB, Pro 34,95 USD):
  https://foldermarker.com/en/folder-marker-pro
- FolderIco: https://www.folderico.com/
- Folderly (geçmiş ve geri alma, Store, 1,49 USD tanıtım fiyatı):
  https://folderlyapp.com/en
- IconStyler (19,99 USD ömür boyu, Icon Guard, Live Folders):
  https://iconstyler.app/pricing
- Fold11 (Store, 1.400+ topluluk simgesi, yerleşik üretici):
  https://apps.microsoft.com/detail/9nn3sbfdk83c
- Sordum Folder Painter: https://www.sordum.org/10124/folder-painter-v1-3/
- Stardock IconPackager: https://www.stardock.com/products/iconpackager/
- Files App (açık kaynak): https://github.com/files-community/Files
- WinSetView (açık kaynak): https://github.com/LesFerch/WinSetView
- Slant, "Folderico vs Dr. Folder" (kullanıcı tercihi karşılaştırması):
  https://slant.co/versus/49299/49302/~folderico_vs_dr-folder
