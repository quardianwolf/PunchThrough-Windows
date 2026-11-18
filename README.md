# PunchThrough for Windows

**One-click DPI bypass tool for Windows.** Unblock restricted websites and services without a VPN. PunchThrough defeats Deep Packet Inspection (DPI) used by ISPs and network providers to block or throttle your traffic.

Works like a local VPN alternative — no remote servers, no subscriptions, no speed loss. No proxy — works at the packet level so games, streaming, and all apps work normally.

[![GitHub](https://img.shields.io/github/v/release/quardianwolf/PunchThrough-Windows?label=Download&style=flat-square)](https://github.com/quardianwolf/PunchThrough-Windows/releases)

---

## How It Works

PunchThrough uses two techniques to bypass internet censorship:

1. **DNS over HTTPS (DoH)** — ISPs poison DNS responses for blocked domains. PunchThrough switches your DNS to Cloudflare's encrypted DoH, so your ISP can't tamper with DNS queries.

2. **TLS Fragmentation** — Uses [zapret](https://github.com/bol-van/zapret) (winws) to fragment TLS ClientHello packets at the network driver level via WinDivert. DPI systems can't inspect fragmented handshakes, so blocks are bypassed.

**No proxy. No tunnel. No speed penalty. Just unblocked internet.**

For sites that block your country themselves (not your ISP), there is an optional [Tunnel](#tunnel-open-sites-that-block-your-country) through a server of your own.

---

## Features

- **One-click connect/disconnect** from the system tray
- **Three bypass modes:**
  - **Full Bypass** — all HTTPS traffic is protected
  - **Discord Only** — only Discord is unblocked, other apps unaffected
  - **Custom** — choose which sites to unblock with preset lists
- **Auto-connect on startup** — set it once, forget about it
- **Built-in installer** — everything is bundled, no downloads needed
- **No system proxy** — works at packet level, games and streaming unaffected
- **DNS over HTTPS** — encrypted DNS via Cloudflare, Google or Quad9 (or your own DNS server)
- **Optional SSH tunnel** — send selected sites, or all browser traffic, through your own server to open sites that block your country
- **Self-healing** — restarts the bypass engine if it dies, and restores your DNS even after a crash
- **Preset domain lists** — Discord, Twitter/X, Instagram, Reddit, TikTok, YouTube, and more
- **Multi-language** — English, Turkish, French
- **Lightweight** — runs silently in the system tray
- **Clean uninstall** — "Reset & Quit" removes the app, its settings and its startup entry

---

## Quick Start

1. Download `PunchThrough.exe` from [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases)
2. Run it as Administrator — the setup screen will handle everything
3. Choose your bypass mode (Full, Discord Only, or Custom)
4. Click **Install**, then **Launch PunchThrough**

That's it. Your internet is now unblocked.

**Note:** Administrator rights are required for the WinDivert network driver and DNS configuration.

---

## Bypass Modes

| Mode | What it does | Best for |
|------|-------------|----------|
| **Full Bypass** | DPI bypass on all HTTPS + system DoH DNS | Unblocking everything |
| **Discord Only** | DPI bypass only for Discord domains | Minimal impact, just Discord |
| **Custom** | DPI bypass for selected domains | Fine-grained control |

In Custom mode, you can add domains manually or use preset buttons: Discord, Twitter/X, Instagram, Reddit, TikTok, YouTube, Wikipedia, Patreon, Adult Sites.

---

## Tunnel: open sites that block your country

DPI bypass defeats blocks made by your ISP. It cannot help when the **website itself** refuses visitors from your country — for that your traffic has to come out somewhere else. The Tunnel does exactly that, using a server you control:

- **Selected sites only** — just the sites you list go through your server; everything else keeps using your normal connection (with DPI bypass).
- **All browser traffic** — every browser goes through your server.

It uses the SSH client built into Windows (`ssh -D`) plus a proxy auto-config script, so **nothing has to be installed on the server**. Any Linux machine you can SSH into works.

### 1. Get a server (skip if you already have one)

Any VPS in a country where the site is reachable will do. A free option is the Oracle Cloud "Always Free" tier:

1. Create an account at a cloud provider and pick a **region outside your country**.
2. Create a Linux instance (Ubuntu is fine).
3. When asked about SSH keys, choose **generate a key pair** and **download the private key**. Keep that file — it is your login.
4. Note the instance's **public IP address**.
5. Make sure **TCP port 22** is open in the provider's firewall / security list (it is by default on most providers).

Login user names: `ubuntu` on Ubuntu images, `opc` on Oracle Linux, `root` on many other VPS providers.

### 2. Set it up in PunchThrough

Open **Settings > Tunnel** and follow the four steps on that page:

| Step | What you do | What PunchThrough checks |
|------|-------------|--------------------------|
| 1. SSH client | Nothing, usually. If it is missing, click **Install**. | That Windows' OpenSSH client is present |
| 2. Your server | Enter address, port (22), user and pick the private key file | That the key is a usable OpenSSH private key (not `.ppk`, not `.pub`) |
| 3. Test the connection | Click **Test connection** | Server reachable → login works → which IP and country your traffic exits from |
| 4. What goes through | Choose a mode, add your sites, click **Save & apply** | — |

Every check tells you what to fix when it fails, and the test stops at the first problem. The tunnel then starts and stops together with Connect/Disconnect and reconnects by itself if the connection drops.

**Adding sites:** type a domain (for example `example.com`) and press **+**. Subdomains are included automatically. If a page opens without images or styling, the site serves those from a second domain — add that one too (your browser's developer tools, Network tab, shows which).

### Good to know

- **Browsers only.** The tunnel is applied through the Windows proxy settings. Chrome, Edge and Firefox (default "use system proxy settings") follow it; games and apps that ignore the system proxy do not. It is not a full VPN.
- **Key login only.** Password logins and keys protected by a passphrase are not supported.
- Your key file is never modified. PunchThrough works on a private copy inside `%LocalAppData%\PunchThrough\tunnel`, because OpenSSH rejects keys that other accounts can read.
- The exit location check asks `cloudflare.com/cdn-cgi/trace` — through the tunnel — which IP it sees.
- Some sites treat data-centre IP addresses with suspicion and may show extra CAPTCHAs.

---

## File Locations

| What | Where |
|------|-------|
| Application | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| Zapret engine | `%LocalAppData%\PunchThrough\zapret\` |
| Settings | `%LocalAppData%\PunchThrough\settings.json` |
| Desktop shortcut | `Desktop\PunchThrough.lnk` |
| Startup entry | Task Scheduler task named `PunchThrough` (runs at logon) |

---

## Uninstall

**From the app:** Right-click the tray icon > **Reset & Quit**. After a confirmation this stops the bypass, restores DNS and proxy settings, and removes the startup task, the desktop shortcut, the settings and the program files.

**Manual removal:**
1. Close PunchThrough from the tray
2. Delete `%LocalAppData%\Programs\PunchThrough\`
3. Delete `%LocalAppData%\PunchThrough\`
4. Delete the desktop shortcut
5. Remove the startup task: open **Task Scheduler**, delete the task named `PunchThrough` (or run `schtasks /Delete /TN PunchThrough /F`)

---

## FAQ

**Is this a VPN?**
No. PunchThrough doesn't encrypt or tunnel your traffic. It fragments TLS handshake packets to prevent DPI systems from identifying and blocking them, and uses encrypted DNS to bypass DNS poisoning.

**Will this slow down my internet?**
No. There's no proxy or tunnel. Zapret works at the packet level — it only touches TLS handshakes, not your actual data.

**Why does it need Administrator?**
The WinDivert driver requires admin to load, and DNS configuration requires admin to change system settings.

**Will this break my games?**
No. Unlike proxy-based solutions, PunchThrough works at the packet level. Games, streaming, and all apps work normally. In Discord Only or Custom mode, only selected domains are affected.

**My internet breaks after closing PunchThrough?**
Use the **Quit** button in the tray menu — it restores DNS automatically. If the process was killed or the PC lost power, just start PunchThrough again: it remembers your original DNS and proxy settings and restores them on startup. To fix it by hand, change DNS back in Windows Settings > Network > DNS, or run: `netsh interface ip set dns "YOUR_ADAPTER" dhcp`

**A site says it is not available in my country — bypass doesn't help?**
That block is made by the website, not your ISP. Use the [Tunnel](#tunnel-open-sites-that-block-your-country) with a server in another country.

---

## Build from Source

```bash
dotnet publish PunchThrough\PunchThrough.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Requires .NET 9 SDK. Zapret (winws.exe) binaries must be placed in `PunchThrough\Assets\`.

---

## Credits

- [zapret](https://github.com/bol-van/zapret) by bol-van — packet-level DPI bypass engine
- [WinDivert](https://reqrypt.org/windivert.html) — Windows network packet capture/divert driver

---

## Keywords

DPI bypass, internet censorship, website unblock, local proxy, VPN alternative, Windows proxy tool, network freedom, anti-censorship, deep packet inspection bypass, free VPN alternative, unblock websites Windows, ISP bypass tool, TLS fragmentation, internet freedom tool, zapret, WinDivert, DNS over HTTPS, DoH

---

---

# PunchThrough Windows

**Windows icin tek tikla DPI bypass araci.** VPN olmadan engellenmis sitelere ve servislere erisin. PunchThrough, ISP'lerin trafigi engellemek icin kullandigi Derin Paket Incelemesini (DPI) paket seviyesinde alt eder.

Yerel VPN alternatifi — uzak sunucu yok, abonelik yok, hiz kaybi yok. Proxy yok — paket seviyesinde calistigi icin oyunlar, streaming ve tum uygulamalar normal calisir.

---

## Nasil Calisir

1. **DNS over HTTPS (DoH)** — ISP'ler engelli sitelerin DNS yanitlarini zehirler. PunchThrough DNS'inizi Cloudflare'in sifreli DoH'una cevirir.
2. **TLS Parcalama** — zapret (winws) kullanarak TLS ClientHello paketlerini WinDivert ile parcalar. DPI sistemleri parcalanmis el sikismalari inceleyemez.

**Proxy yok. Tunel yok. Hiz kaybi yok. Sadece engelsiz internet.**

---

## Ozellikler

- **Tek tikla baglan/kes** — sistem tepsisinden
- **Uc bypass modu:**
  - **Tam Bypass** — tum HTTPS trafigi korunur
  - **Sadece Discord** — sadece Discord engeli kaldirilir
  - **Ozel** — hangi sitelerin engelini kaldirmak istedigini sec
- **Baslangicta otomatik baglanma**
- **Dahili kurulum** — her sey gomulu, indirme gerekmez
- **Sistem proxy'si yok** — paket seviyesinde calisir, oyunlar etkilenmez
- **DNS over HTTPS** — otomatik sifreli DNS
- **Hazir domain listeleri** — Discord, Twitter/X, Instagram, Reddit, TikTok, YouTube ve dahasi
- **Cok dilli** — Ingilizce, Turkce, Fransizca
- **Temiz kaldirma** — "Sifirla & Cik" ile tum izleri siler

---

## Hizli Baslangic

1. [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases) sayfasindan `PunchThrough.exe` indirin
2. Yonetici olarak calistirin — kurulum ekrani her seyi halleder
3. Bypass modunuzu secin (Tam, Sadece Discord veya Ozel)
4. **Install** ve ardindan **Launch PunchThrough** tiklayin

Bu kadar. Internetiniz artik engelsiz.

---

## Tunel: ulkenizi engelleyen siteleri acma

DPI bypass, ISP'nizin koydugu engelleri asar. **Sitenin kendisi** ulkenizden gelenleri engelliyorsa ise yaramaz — bunun icin trafiginizin baska bir yerden cikmasi gerekir. Tunel tam olarak bunu, kendi kontrolunuzdeki bir sunucuyla yapar:

- **Sadece secili siteler** — yalnizca listeye eklediginiz siteler sunucunuzdan gecer; geri kalan her sey normal baglantinizda (DPI bypass ile) kalir.
- **Tum tarayici trafigi** — butun tarayicilar sunucunuz uzerinden cikar.

Windows'un dahili SSH istemcisini (`ssh -D`) ve bir proxy otomatik yapilandirma betigini kullanir; **sunucuya hicbir sey kurmaniz gerekmez**. SSH ile girebildiginiz her Linux makine olur.

### 1. Sunucu edinin (zaten varsa atlayin)

Sitenin acik oldugu bir ulkedeki herhangi bir VPS yeterli. Ucretsiz bir secenek Oracle Cloud "Always Free" katmanidir:

1. Bir bulut saglayicida hesap acin ve **ulkeniz disinda bir bolge** secin.
2. Bir Linux sunucu olusturun (Ubuntu uygundur).
3. SSH anahtari soruldugunda **anahtar cifti olustur** deyin ve **ozel anahtari (private key) indirin**. Bu dosyayi saklayin — giris bilginiz odur.
4. Sunucunun **genel (public) IP adresini** not edin.
5. Saglayicinin guvenlik duvarinda / security list'inde **TCP 22 portunun** acik oldugundan emin olun (cogu saglayicida varsayilan olarak aciktir).

Giris kullanici adlari: Ubuntu imajlarinda `ubuntu`, Oracle Linux'ta `opc`, bircok diger VPS saglayicisinda `root`.

### 2. PunchThrough'da ayarlayin

**Ayarlar > Tunel** sekmesini acin ve sayfadaki dort adimi izleyin:

| Adim | Siz ne yaparsiniz | PunchThrough neyi kontrol eder |
|------|-------------------|--------------------------------|
| 1. SSH istemcisi | Genelde hicbir sey. Eksikse **Kur**'a tiklayin. | Windows OpenSSH istemcisi kurulu mu |
| 2. Sunucunuz | Adres, port (22), kullanici girin ve ozel anahtar dosyasini secin | Anahtar kullanilabilir bir OpenSSH ozel anahtari mi (`.ppk` veya `.pub` degil) |
| 3. Baglantiyi test edin | **Baglantiyi test et**'e tiklayin | Sunucuya ulasiliyor mu → giris calisiyor mu → trafik hangi IP ve ulkeden cikiyor |
| 4. Tunelden ne gecsin | Modu secin, sitelerinizi ekleyin, **Kaydet & uygula**'ya tiklayin | — |

Her kontrol basarisiz oldugunda neyi duzeltmeniz gerektigini soyler ve test ilk sorunda durur. Tunel, Baglan/Baglantiyi Kes ile birlikte acilip kapanir; baglanti koparsa kendiliginden yeniden baglanir.

**Site ekleme:** alan adini yazin (ornegin `example.com`) ve **+**'ya basin. Alt alan adlari otomatik dahildir. Sayfa resimsiz veya bozuk aciliyorsa site bunlari ikinci bir alan adindan sunuyordur — onu da ekleyin (tarayicinin gelistirici araclarindaki Network sekmesi hangisi oldugunu gosterir).

### Bilmeniz gerekenler

- **Sadece tarayicilar.** Tunel, Windows proxy ayarlari uzerinden uygulanir. Chrome, Edge ve Firefox (varsayilan "sistem proxy ayarlarini kullan") buna uyar; oyunlar ve sistem proxy'sini yok sayan uygulamalar uymaz. Tam bir VPN degildir.
- **Sadece anahtarla giris.** Parolayla giris ve parola korumali anahtarlar desteklenmez.
- Anahtar dosyaniz degistirilmez. OpenSSH baska hesaplarin okuyabildigi anahtarlari reddettigi icin PunchThrough, `%LocalAppData%\PunchThrough\tunnel` icindeki ozel bir kopya uzerinde calisir.
- Cikis konumu kontrolu, tunel uzerinden `cloudflare.com/cdn-cgi/trace` adresine hangi IP'yi gordugunu sorar.
- Bazi siteler veri merkezi IP adreslerine supheyle yaklasir ve ek CAPTCHA gosterebilir.

---

## Dosya Konumlari

| Ne | Nerede |
|----|--------|
| Uygulama | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| Zapret motoru | `%LocalAppData%\PunchThrough\zapret\` |
| Ayarlar | `%LocalAppData%\PunchThrough\settings.json` |
| Masaustu kisayolu | `Masaustu\PunchThrough.lnk` |

---

## Kaldirma

**Uygulamadan:** Tepsi simgesine sag tikla > **Sifirla & Cik**. Onay aldiktan sonra bypass'i durdurur, DNS ve proxy ayarlarini geri yukler; baslangic gorevini, masaustu kisayolunu, ayarlari ve program dosyalarini siler.

**Manuel kaldirma:**
1. PunchThrough'u tepsiden kapatin
2. `%LocalAppData%\Programs\PunchThrough\` silin
3. `%LocalAppData%\PunchThrough\` silin
4. Masaustu kisayolunu silin
5. Baslangic gorevini silin: **Gorev Zamanlayici**'da `PunchThrough` gorevini silin (veya `schtasks /Delete /TN PunchThrough /F`)

---

## SSS

**Bu bir VPN mi?**
Hayir. Sadece TLS el sikisma paketlerini parcalar ve sifreli DNS kullanir. Trafiginiz sifrelenmez veya tunellenmez.

**Oyunlarimi bozar mi?**
Hayir. Proxy tabanli cozumlerden farkli olarak paket seviyesinde calisir. Oyunlar, streaming ve tum uygulamalar normal calisir.

**Neden Yonetici izni gerekiyor?**
WinDivert ag surucusu ve DNS yapilandirmasi icin yonetici hakki gereklidir.

---

---

# PunchThrough pour Windows

**Outil de contournement DPI en un clic pour Windows.** Debloquez les sites et services restreints sans VPN. PunchThrough contourne l'Inspection Approfondie des Paquets (DPI) au niveau des paquets reseau.

Alternative VPN locale — pas de serveur distant, pas d'abonnement, pas de perte de vitesse. Pas de proxy — fonctionne au niveau des paquets, les jeux et le streaming ne sont pas affectes.

---

## Comment ca marche

1. **DNS over HTTPS (DoH)** — Les FAI empoisonnent les reponses DNS. PunchThrough bascule vers le DoH chiffre de Cloudflare.
2. **Fragmentation TLS** — Utilise zapret (winws) pour fragmenter les paquets TLS ClientHello via WinDivert.

**Pas de proxy. Pas de tunnel. Pas de ralentissement. Juste un internet debloque.**

---

## Fonctionnalites

- **Connexion/deconnexion en un clic**
- **Trois modes de bypass :**
  - **Bypass complet** — tout le trafic HTTPS est protege
  - **Discord uniquement** — seul Discord est debloque
  - **Personnalise** — choisissez quels sites debloquer
- **Connexion automatique au demarrage**
- **Installateur integre** — tout est inclus
- **Pas de proxy systeme** — fonctionne au niveau des paquets
- **DNS over HTTPS** — DNS chiffre automatique
- **Multilingue** — anglais, turc, francais
- **Desinstallation propre** — "Reinitialiser & Quitter"

---

## Demarrage rapide

1. Telechargez `PunchThrough.exe` depuis [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases)
2. Executez en tant qu'administrateur
3. Choisissez votre mode de bypass
4. Cliquez sur **Install**, puis **Launch PunchThrough**

---

## Tunnel : ouvrir les sites qui bloquent votre pays

Le bypass DPI contourne les blocages de votre FAI. Il ne peut rien quand c'est **le site lui-meme** qui refuse votre pays. Le Tunnel fait sortir ce trafic par un serveur que vous controlez, via le client SSH integre a Windows — rien a installer sur le serveur.

1. Procurez-vous un serveur Linux accessible en SSH dans un autre pays (par exemple une VM gratuite Oracle Cloud) et telechargez sa cle privee.
2. Ouvrez **Parametres > Tunnel** et suivez les quatre etapes : client SSH, informations du serveur, **Tester la connexion** (serveur joignable → connexion → IP et pays de sortie), puis choisissez **Sites selectionnes uniquement** ou **Tout le trafic des navigateurs** et cliquez sur **Enregistrer & appliquer**.

Seuls les navigateurs (proxy systeme Windows) sont concernes ; ce n'est pas un VPN complet. Connexion par cle uniquement (pas de mot de passe ni de phrase secrete). Le guide detaille se trouve dans la section anglaise ci-dessus.

---

## Desinstallation

Clic droit sur l'icone > **Reinitialiser & Quitter**. Apres confirmation : arrete le bypass, restaure le DNS, supprime la tache de demarrage, le raccourci, les parametres et les fichiers du programme.

---

## Credits

- [zapret](https://github.com/bol-van/zapret) par bol-van
- [WinDivert](https://reqrypt.org/windivert.html) par basil00

---

## License

MIT
