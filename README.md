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
- **DNS over HTTPS** — automatic encrypted DNS via Cloudflare
- **Preset domain lists** — Discord, Twitter/X, Instagram, Reddit, TikTok, YouTube, and more
- **Multi-language** — English, Turkish, French
- **Lightweight** — runs silently in the system tray
- **Clean uninstall** — "Reset & Quit" removes all traces

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

## File Locations

| What | Where |
|------|-------|
| Application | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| Zapret engine | `%LocalAppData%\PunchThrough\zapret\` |
| Settings | `%LocalAppData%\PunchThrough\settings.json` |
| Desktop shortcut | `Desktop\PunchThrough.lnk` |
| Startup entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

---

## Uninstall

**From the app:** Right-click the tray icon > **Reset & Quit**. This stops the bypass, restores DNS, removes the startup entry, and you can delete the files manually.

**Manual removal:**
1. Close PunchThrough from the tray
2. Delete `%LocalAppData%\Programs\PunchThrough\`
3. Delete `%LocalAppData%\PunchThrough\`
4. Delete the desktop shortcut
5. Remove the startup entry: open `regedit`, go to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, delete `PunchThrough`

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
Use the **Quit** button in the tray menu — it restores DNS automatically. If you killed the process directly, your DNS might still be set to Cloudflare (1.1.1.1). Change it back in Windows Settings > Network > DNS, or run: `netsh interface ip set dns "YOUR_ADAPTER" dhcp`

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

## Dosya Konumlari

| Ne | Nerede |
|----|--------|
| Uygulama | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| Zapret motoru | `%LocalAppData%\PunchThrough\zapret\` |
| Ayarlar | `%LocalAppData%\PunchThrough\settings.json` |
| Masaustu kisayolu | `Masaustu\PunchThrough.lnk` |

---

## Kaldirma

**Uygulamadan:** Tepsi simgesine sag tikla > **Sifirla & Cik**. Bypass'i durdurur, DNS'i geri yukler, baslangic kaydini siler.

**Manuel kaldirma:**
1. PunchThrough'u tepsiden kapatin
2. `%LocalAppData%\Programs\PunchThrough\` silin
3. `%LocalAppData%\PunchThrough\` silin
4. Masaustu kisayolunu silin

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

## Desinstallation

Clic droit sur l'icone > **Reinitialiser & Quitter**. Arrete le bypass, restaure le DNS, supprime l'entree de demarrage.

---

## Credits

- [zapret](https://github.com/bol-van/zapret) par bol-van
- [WinDivert](https://reqrypt.org/windivert.html) par basil00

---

## License

MIT
