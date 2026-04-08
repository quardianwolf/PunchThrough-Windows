# PunchThrough for Windows

**One-click DPI bypass tool for Windows.** Unblock restricted websites and services without a VPN. PunchThrough acts as a local proxy that defeats Deep Packet Inspection (DPI) used by ISPs and network providers to block or throttle your traffic.

Works like a local VPN alternative — no remote servers, no subscriptions, no speed loss. Your traffic stays yours.

[![GitHub](https://img.shields.io/github/v/release/quardianwolf/PunchThrough-Windows?label=Download&style=flat-square)](https://github.com/quardianwolf/PunchThrough-Windows/releases)

---

## How It Works

PunchThrough runs [SpoofDPI](https://github.com/xvzc/SpoofDPI) as a local proxy and configures your system to route traffic through it. SpoofDPI fragments TLS handshake packets so that DPI systems can't inspect them — bypassing blocks without encrypting or rerouting your traffic.

**No VPN. No tunnel. No speed penalty. Just unblocked internet.**

---

## Features

- **One-click connect/disconnect** from the system tray
- **Auto-connect on startup** — set it once, forget about it
- **Built-in installer** — downloads everything it needs on first run
- **System proxy integration** — all apps benefit automatically
- **DNS options** — Google, Cloudflare, Quad9, or custom DNS
- **DNS over HTTPS (DoH)** support
- **Multi-language** — English, Turkish, French
- **Lightweight** — runs silently in the system tray
- **Clean uninstall** — "Reset & Quit" removes all traces

---

## Quick Start

1. Download `PunchThrough.exe` from [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases)
2. Run it — the setup screen will handle everything
3. Click **Launch PunchThrough**
4. Right-click the tray icon and hit **Connect**

That's it. Your internet is now unblocked.

---

## File Locations

| What | Where |
|------|-------|
| Application | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| SpoofDPI binary | `%LocalAppData%\PunchThrough\bin\spoofdpi.exe` |
| Settings | `%LocalAppData%\PunchThrough\settings.json` |
| Desktop shortcut | `Desktop\PunchThrough.lnk` |
| Startup entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

---

## Uninstall

**From the app:** Right-click the tray icon > **Reset & Quit**. This stops SpoofDPI, clears proxy settings, removes the startup entry, and you can delete the files manually.

**Manual removal:**
1. Close PunchThrough from the tray
2. Delete `%LocalAppData%\Programs\PunchThrough\`
3. Delete `%LocalAppData%\PunchThrough\`
4. Delete the desktop shortcut
5. Remove the startup entry: open `regedit`, go to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, delete `PunchThrough`

---

## FAQ

**Is this a VPN?**
No. PunchThrough doesn't encrypt or tunnel your traffic. It only fragments TLS handshake packets to prevent DPI systems from identifying and blocking them. Your ISP can still see which sites you visit — they just can't block them via DPI.

**Will this slow down my internet?**
No. Traffic goes through a local proxy on your own machine. There's no remote server in the middle.

**Windows Defender is using high CPU?**
PunchThrough automatically adds a Defender exclusion on first connect. If you skipped the UAC prompt, you can add it manually:
```powershell
# Run as Administrator
Add-MpPreference -ExclusionProcess "spoofdpi.exe"
```

**My internet breaks after closing PunchThrough?**
Use the **Quit** button in the tray menu — it cleans up proxy settings automatically. If you killed the process directly, open Windows Settings > Network > Proxy and turn off manual proxy.

---

## Build from Source

```bash
# Build
dotnet publish PunchThrough\PunchThrough.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Output
PunchThrough\bin\Release\net9.0-windows\win-x64\publish\PunchThrough.exe
```

Requires .NET 9 SDK.

---

## Keywords

DPI bypass, internet censorship, website unblock, SpoofDPI, local proxy, VPN alternative, Windows proxy tool, network freedom, anti-censorship, deep packet inspection bypass, free VPN alternative, unblock websites Windows, ISP bypass tool, TLS fragmentation, internet freedom tool

---

---

# PunchThrough Windows

**Windows icin tek tikla DPI bypass araci.** VPN olmadan engellenmis sitelere ve servislere erisin. PunchThrough, ISP'lerin ve ag saglayicilarinin trafiginizi engellemek icin kullandigi Derin Paket Incelemesini (DPI) alt eden yerel bir proxy olarak calisir.

Yerel VPN alternatifi gibi calisir — uzak sunucu yok, abonelik yok, hiz kaybi yok.

---

## Nasil Calisir

PunchThrough, [SpoofDPI](https://github.com/xvzc/SpoofDPI)'yi yerel proxy olarak calistirir ve sisteminizi trafigi onun uzerinden yonlendirecek sekilde ayarlar. SpoofDPI, TLS el sikisma paketlerini parcalayarak DPI sistemlerinin bunlari incelemesini engeller.

**VPN yok. Tunel yok. Hiz kaybi yok. Sadece engelsiz internet.**

---

## Ozellikler

- **Tek tikla baglan/kes** — sistem tepsisinden
- **Baslangicta otomatik baglanma** — bir kez ayarla, unut gitsin
- **Dahili kurulum** — ilk calistirmada her seyi kendisi indirip kurar
- **Sistem proxy entegrasyonu** — tum uygulamalar otomatik faydalanir
- **DNS secenekleri** — Google, Cloudflare, Quad9 veya ozel DNS
- **DNS over HTTPS (DoH)** destegi
- **Cok dilli** — Ingilizce, Turkce, Fransizca
- **Hafif** — sistem tepsisinde sessizce calisir
- **Temiz kaldirma** — "Sifirla & Cik" ile tum izleri siler

---

## Hizli Baslangic

1. [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases) sayfasindan `PunchThrough.exe` indirin
2. Calistirin — kurulum ekrani her seyi halleder
3. **Launch PunchThrough** butonuna basin
4. Tepsi simgesine sag tiklayin ve **Baglan** deyin

Bu kadar. Internetiniz artik engelsiz.

---

## Dosya Konumlari

| Ne | Nerede |
|----|--------|
| Uygulama | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| SpoofDPI | `%LocalAppData%\PunchThrough\bin\spoofdpi.exe` |
| Ayarlar | `%LocalAppData%\PunchThrough\settings.json` |
| Masaustu kisayolu | `Masaustu\PunchThrough.lnk` |
| Baslangic kaydi | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

---

## Kaldirma

**Uygulamadan:** Tepsi simgesine sag tikla > **Sifirla & Cik**. SpoofDPI'yi durdurur, proxy ayarlarini temizler, baslangic kaydini siler.

**Manuel kaldirma:**
1. PunchThrough'u tepsiden kapatin
2. `%LocalAppData%\Programs\PunchThrough\` klasorunu silin
3. `%LocalAppData%\PunchThrough\` klasorunu silin
4. Masaustu kisayolunu silin
5. Baslangic kaydini silin: `regedit` > `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` > `PunchThrough`'u silin

---

## SSS

**Bu bir VPN mi?**
Hayir. PunchThrough trafiginizi sifrelemiyor veya tunellemiyor. Sadece TLS el sikisma paketlerini parcalayarak DPI sistemlerinin bunlari tanimlamasini ve engellemesini onler.

**Internetimi yavaslatir mi?**
Hayir. Trafik kendi makinenizdeki yerel bir proxy uzerinden gecer. Arada uzak sunucu yok.

**Windows Defender yuksek CPU kullaniyor?**
PunchThrough ilk baglantiginda otomatik olarak Defender istisnasi ekler. UAC isteini atladiysan manuel ekleyebilirsin:
```powershell
# Yonetici olarak calistirin
Add-MpPreference -ExclusionProcess "spoofdpi.exe"
```

**PunchThrough'u kapattiktan sonra internet calismiyorsa?**
Tepsi menusundeki **Cikis** butonunu kullanin — proxy ayarlarini otomatik temizler. Islemi dogrudan sonlandirdiysan: Windows Ayarlar > Ag > Proxy > Manuel proxy'yi kapatin.

---

---

# PunchThrough pour Windows

**Outil de contournement DPI en un clic pour Windows.** Debloquez les sites et services restreints sans VPN. PunchThrough fonctionne comme un proxy local qui contourne l'Inspection Approfondie des Paquets (DPI) utilisee par les FAI et les fournisseurs de reseau.

Fonctionne comme une alternative VPN locale — pas de serveur distant, pas d'abonnement, pas de perte de vitesse.

---

## Comment ca marche

PunchThrough execute [SpoofDPI](https://github.com/xvzc/SpoofDPI) comme proxy local et configure votre systeme pour acheminer le trafic a travers lui. SpoofDPI fragmente les paquets de handshake TLS pour que les systemes DPI ne puissent pas les inspecter.

**Pas de VPN. Pas de tunnel. Pas de ralentissement. Juste un internet debloque.**

---

## Fonctionnalites

- **Connexion/deconnexion en un clic** depuis la barre systeme
- **Connexion automatique au demarrage** — configurez une fois, oubliez
- **Installateur integre** — telecharge tout ce qu'il faut au premier lancement
- **Integration proxy systeme** — toutes les applications en beneficient
- **Options DNS** — Google, Cloudflare, Quad9 ou DNS personnalise
- **Support DNS over HTTPS (DoH)**
- **Multilingue** — anglais, turc, francais
- **Leger** — fonctionne silencieusement dans la barre systeme
- **Desinstallation propre** — "Reinitialiser & Quitter" supprime toutes les traces

---

## Demarrage rapide

1. Telechargez `PunchThrough.exe` depuis [Releases](https://github.com/quardianwolf/PunchThrough-Windows/releases)
2. Executez-le — l'ecran de configuration s'occupe de tout
3. Cliquez sur **Launch PunchThrough**
4. Clic droit sur l'icone de la barre systeme et cliquez sur **Connecter**

C'est tout. Votre internet est maintenant debloque.

---

## Emplacements des fichiers

| Quoi | Ou |
|------|-----|
| Application | `%LocalAppData%\Programs\PunchThrough\PunchThrough.exe` |
| Binaire SpoofDPI | `%LocalAppData%\PunchThrough\bin\spoofdpi.exe` |
| Parametres | `%LocalAppData%\PunchThrough\settings.json` |
| Raccourci bureau | `Bureau\PunchThrough.lnk` |
| Entree demarrage | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

---

## Desinstallation

**Depuis l'application :** Clic droit sur l'icone > **Reinitialiser & Quitter**. Cela arrete SpoofDPI, nettoie les parametres proxy et supprime l'entree de demarrage.

**Suppression manuelle :**
1. Fermez PunchThrough depuis la barre systeme
2. Supprimez `%LocalAppData%\Programs\PunchThrough\`
3. Supprimez `%LocalAppData%\PunchThrough\`
4. Supprimez le raccourci bureau
5. Supprimez l'entree de demarrage : `regedit` > `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` > supprimez `PunchThrough`

---

## FAQ

**C'est un VPN ?**
Non. PunchThrough ne chiffre ni ne tunnelise votre trafic. Il fragmente uniquement les paquets de handshake TLS pour empecher les systemes DPI de les identifier et de les bloquer.

**Ca va ralentir mon internet ?**
Non. Le trafic passe par un proxy local sur votre propre machine. Aucun serveur distant implique.

**Windows Defender utilise beaucoup de CPU ?**
PunchThrough ajoute automatiquement une exclusion Defender a la premiere connexion. Si vous avez refuse l'invite UAC, ajoutez-la manuellement :
```powershell
# Executer en tant qu'administrateur
Add-MpPreference -ExclusionProcess "spoofdpi.exe"
```

**Mon internet ne marche plus apres avoir ferme PunchThrough ?**
Utilisez le bouton **Quitter** dans le menu de la barre systeme — il nettoie automatiquement les parametres proxy. Si vous avez tue le processus directement : Parametres Windows > Reseau > Proxy > desactivez le proxy manuel.

---

## Compilation depuis les sources

```bash
# Compiler
dotnet publish PunchThrough\PunchThrough.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Resultat
PunchThrough\bin\Release\net9.0-windows\win-x64\publish\PunchThrough.exe
```

Necessite .NET 9 SDK.

---

## Mots-cles

Contournement DPI, censure internet, debloquer sites, SpoofDPI, proxy local, alternative VPN, outil proxy Windows, liberte internet, anti-censure, contournement inspection paquets, alternative VPN gratuite, debloquer sites Windows, outil contournement FAI, fragmentation TLS, outil liberte internet

---

## License

MIT

