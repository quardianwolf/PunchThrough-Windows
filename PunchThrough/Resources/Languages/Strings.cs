using System.Collections.Generic;
using System.Globalization;

namespace PunchThrough.Models;

public static class Strings
{
    private static CultureInfo _culture = CultureInfo.CurrentUICulture;

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["Connected"] = "Connected",
            ["Connecting"] = "Connecting...",
            ["Disconnecting"] = "Disconnecting...",
            ["Disconnected"] = "Disconnected",
            ["Error"] = "Error",
            ["Connect"] = "Connect",
            ["Disconnect"] = "Disconnect",

            ["General"] = "General",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Logs",
            ["LaunchAtStartup"] = "Launch at Startup",
            ["AutoConnect"] = "Auto-connect on startup",
            ["Connection"] = "Connection",
            ["Status"] = "Status",
            ["Method"] = "Method",
            ["DNS"] = "DNS",
            ["About"] = "About",
            ["GitHubRepository"] = "GitHub Repository",
            ["Language"] = "Language",
            ["RestartLanguage"] = "Restart the app to apply language changes.",
            ["DnsServer"] = "DNS Server",
            ["Custom"] = "Custom",
            ["UseDoH"] = "Use DNS over HTTPS (DoH)",

            ["NoLogs"] = "No Logs",
            ["ClearLogs"] = "Clear Logs",
            ["Entries"] = "entries",

            ["ProxyModeFull"] = "Full Bypass",
            ["ProxyModeDiscord"] = "Discord Only",
            ["ProxyModeCustom"] = "Custom",
            ["ProxyModeFullDesc"] = "All HTTPS traffic is protected. Unblocks everything.",
            ["ProxyModeDiscordDesc"] = "Only Discord is unblocked. Other apps unaffected.",
            ["ProxyModeCustomDesc"] = "Choose which sites to unblock.",

            ["BypassStrategy"] = "Bypass Strategy",
            ["AutoDetectStrategy"] = "Auto-detect best strategy",

            ["ShowSettings"] = "Settings",
            ["ResetAndQuit"] = "Reset && Quit",
            ["Quit"] = "Quit",
        },

        ["tr"] = new()
        {
            ["Connected"] = "Baglandi",
            ["Connecting"] = "Baglaniyor...",
            ["Disconnecting"] = "Baglanti kesiliyor...",
            ["Disconnected"] = "Bagli degil",
            ["Error"] = "Hata",
            ["Connect"] = "Baglan",
            ["Disconnect"] = "Baglantiyi Kes",

            ["General"] = "Genel",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Gunlukler",
            ["LaunchAtStartup"] = "Bilgisayar acildiginda baslat",
            ["AutoConnect"] = "Baslangicta otomatik baglan",
            ["Connection"] = "Baglanti",
            ["Status"] = "Durum",
            ["Method"] = "Yontem",
            ["DNS"] = "DNS",
            ["About"] = "Hakkinda",
            ["GitHubRepository"] = "GitHub Deposu",
            ["Language"] = "Dil",
            ["RestartLanguage"] = "Dil degisikliklerini uygulamak icin uygulamayi yeniden baslatin.",
            ["DnsServer"] = "DNS Sunucusu",
            ["Custom"] = "Ozel",
            ["UseDoH"] = "HTTPS uzerinden DNS kullan (DoH)",

            ["NoLogs"] = "Gunluk Yok",
            ["ClearLogs"] = "Gunlukleri Temizle",
            ["Entries"] = "kayit",

            ["ProxyModeFull"] = "Tam Bypass",
            ["ProxyModeDiscord"] = "Sadece Discord",
            ["ProxyModeCustom"] = "Ozel",
            ["ProxyModeFullDesc"] = "Tum HTTPS trafigi korunur. Her seyi engelsiz acar.",
            ["ProxyModeDiscordDesc"] = "Sadece Discord engeli kaldirilir. Diger uygulamalar etkilenmez.",
            ["ProxyModeCustomDesc"] = "Hangi sitelerin engelini kaldirmak istedigini sec.",

            ["BypassStrategy"] = "Bypass Stratejisi",
            ["AutoDetectStrategy"] = "En iyi stratejiyi otomatik bul",

            ["ShowSettings"] = "Ayarlar",
            ["ResetAndQuit"] = "Sifirla && Cik",
            ["Quit"] = "Cikis",
        },

        ["fr"] = new()
        {
            ["Connected"] = "Connect\u00e9",
            ["Connecting"] = "Connexion...",
            ["Disconnecting"] = "D\u00e9connexion...",
            ["Disconnected"] = "D\u00e9connect\u00e9",
            ["Error"] = "Erreur",
            ["Connect"] = "Connecter",
            ["Disconnect"] = "D\u00e9connecter",

            ["General"] = "G\u00e9n\u00e9ral",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Journaux",
            ["LaunchAtStartup"] = "Lancer au d\u00e9marrage",
            ["AutoConnect"] = "Connexion automatique au d\u00e9marrage",
            ["Connection"] = "Connexion",
            ["Status"] = "Statut",
            ["Method"] = "M\u00e9thode",
            ["DNS"] = "DNS",
            ["About"] = "\u00c0 propos",
            ["GitHubRepository"] = "D\u00e9p\u00f4t GitHub",
            ["Language"] = "Langue",
            ["RestartLanguage"] = "Red\u00e9marrez l'application pour appliquer les changements de langue.",
            ["DnsServer"] = "Serveur DNS",
            ["Custom"] = "Personnalis\u00e9",
            ["UseDoH"] = "Utiliser DNS sur HTTPS (DoH)",

            ["NoLogs"] = "Aucun journal",
            ["ClearLogs"] = "Effacer les journaux",
            ["Entries"] = "entr\u00e9es",

            ["ProxyModeFull"] = "Bypass complet",
            ["ProxyModeDiscord"] = "Discord uniquement",
            ["ProxyModeCustom"] = "Personnalis\u00e9",
            ["ProxyModeFullDesc"] = "Tout le trafic HTTPS est prot\u00e9g\u00e9. D\u00e9bloque tout.",
            ["ProxyModeDiscordDesc"] = "Seul Discord est d\u00e9bloqu\u00e9. Les autres apps ne sont pas affect\u00e9es.",
            ["ProxyModeCustomDesc"] = "Choisissez quels sites d\u00e9bloquer.",

            ["ShowSettings"] = "Param\u00e8tres",
            ["ResetAndQuit"] = "R\u00e9initialiser && Quitter",
            ["Quit"] = "Quitter",
        }
    };

    public static void SetCulture(CultureInfo culture) => _culture = culture;

    public static string Get(string key)
    {
        var lang = _culture.TwoLetterISOLanguageName;
        if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        if (Translations["en"].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }
}
