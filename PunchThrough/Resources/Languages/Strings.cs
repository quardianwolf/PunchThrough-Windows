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
            // General
            ["Connected"] = "Connected",
            ["Connecting"] = "Connecting...",
            ["Disconnecting"] = "Disconnecting...",
            ["Disconnected"] = "Disconnected",
            ["Error"] = "Error",
            ["Connect"] = "Connect",
            ["Disconnect"] = "Disconnect",

            // Menu
            ["Settings"] = "Settings...",
            ["QuitPunchThrough"] = "Quit PunchThrough",

            // Settings - General
            ["General"] = "General",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Logs",
            ["Startup"] = "Startup",
            ["LaunchAtStartup"] = "Launch at Startup",
            ["Connection"] = "Connection",
            ["Status"] = "Status",
            ["Method"] = "Method",
            ["DNS"] = "DNS",
            ["About"] = "About",
            ["GitHubRepository"] = "GitHub Repository",
            ["Language"] = "Language",
            ["SystemDefault"] = "System Default",
            ["RestartLanguage"] = "Restart the app to apply language changes.",

            // Settings - Bypass
            ["BypassMethod"] = "Bypass Method",
            ["DnsServer"] = "DNS Server",
            ["Custom"] = "Custom",
            ["CustomDns"] = "Custom DNS",
            ["UseDoH"] = "Use DNS over HTTPS (DoH)",
            ["Port"] = "Port",
            ["ConfigureSystemProxy"] = "Configure System Proxy",
            ["SystemProxyDesc"] = "System proxy will route traffic through SpoofDPI automatically.",
            ["InstallationStatus"] = "Installation Status",
            ["Installed"] = "Installed",
            ["NotInstalled"] = "Not Installed",
            ["SpoofDPIDesc"] = "Lightweight DPI bypass proxy. Recommended for most users.",
            ["InstallScoopHint"] = "Install via Scoop: scoop install spoofdpi",

            // Settings - Logs
            ["NoLogs"] = "No Logs",
            ["LogsDesc"] = "Connection logs will appear here.",
            ["ClearLogs"] = "Clear Logs",
            ["Entries"] = "entries",

            // Tray
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

            ["Settings"] = "Ayarlar...",
            ["QuitPunchThrough"] = "PunchThrough'dan Cik",

            ["General"] = "Genel",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Gunlukler",
            ["Startup"] = "Baslangic",
            ["LaunchAtStartup"] = "Bilgisayar acildiginda baslat",
            ["Connection"] = "Baglanti",
            ["Status"] = "Durum",
            ["Method"] = "Yontem",
            ["DNS"] = "DNS",
            ["About"] = "Hakkinda",
            ["GitHubRepository"] = "GitHub Deposu",
            ["Language"] = "Dil",
            ["SystemDefault"] = "Sistem Varsayilani",
            ["RestartLanguage"] = "Dil degisikliklerini uygulamak icin uygulamayi yeniden baslatin.",

            ["BypassMethod"] = "Bypass Yontemi",
            ["DnsServer"] = "DNS Sunucusu",
            ["Custom"] = "Ozel",
            ["CustomDns"] = "Ozel DNS",
            ["UseDoH"] = "HTTPS uzerinden DNS kullan (DoH)",
            ["Port"] = "Port",
            ["ConfigureSystemProxy"] = "Sistem Proxy'sini Yapilandir",
            ["SystemProxyDesc"] = "Sistem proxy'si trafigi otomatik olarak SpoofDPI uzerinden yonlendirir.",
            ["InstallationStatus"] = "Kurulum Durumu",
            ["Installed"] = "Kurulu",
            ["NotInstalled"] = "Kurulu Degil",
            ["SpoofDPIDesc"] = "Hafif DPI bypass proxy. Cogu kullanici icin onerilir.",
            ["InstallScoopHint"] = "Scoop ile kurun: scoop install spoofdpi",

            ["NoLogs"] = "Gunluk Yok",
            ["LogsDesc"] = "Baglanti gunlukleri burada gorunecek.",
            ["ClearLogs"] = "Gunlukleri Temizle",
            ["Entries"] = "kayit",

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

            ["Settings"] = "Param\u00e8tres...",
            ["QuitPunchThrough"] = "Quitter PunchThrough",

            ["General"] = "G\u00e9n\u00e9ral",
            ["Bypass"] = "Bypass",
            ["Logs"] = "Journaux",
            ["Startup"] = "D\u00e9marrage",
            ["LaunchAtStartup"] = "Lancer au d\u00e9marrage",
            ["Connection"] = "Connexion",
            ["Status"] = "Statut",
            ["Method"] = "M\u00e9thode",
            ["DNS"] = "DNS",
            ["About"] = "\u00c0 propos",
            ["GitHubRepository"] = "D\u00e9p\u00f4t GitHub",
            ["Language"] = "Langue",
            ["SystemDefault"] = "Par d\u00e9faut du syst\u00e8me",
            ["RestartLanguage"] = "Red\u00e9marrez l'application pour appliquer les changements de langue.",

            ["BypassMethod"] = "M\u00e9thode de bypass",
            ["DnsServer"] = "Serveur DNS",
            ["Custom"] = "Personnalis\u00e9",
            ["CustomDns"] = "DNS personnalis\u00e9",
            ["UseDoH"] = "Utiliser DNS sur HTTPS (DoH)",
            ["Port"] = "Port",
            ["ConfigureSystemProxy"] = "Configurer le proxy syst\u00e8me",
            ["SystemProxyDesc"] = "Le proxy syst\u00e8me acheminera le trafic via SpoofDPI automatiquement.",
            ["InstallationStatus"] = "Statut d'installation",
            ["Installed"] = "Install\u00e9",
            ["NotInstalled"] = "Non install\u00e9",
            ["SpoofDPIDesc"] = "Proxy l\u00e9ger pour contourner le DPI. Recommand\u00e9 pour la plupart des utilisateurs.",
            ["InstallScoopHint"] = "Installer via Scoop : scoop install spoofdpi",

            ["NoLogs"] = "Aucun journal",
            ["LogsDesc"] = "Les journaux de connexion appara\u00eetront ici.",
            ["ClearLogs"] = "Effacer les journaux",
            ["Entries"] = "entr\u00e9es",

            ["ShowSettings"] = "Param\u00e8tres",
            ["ResetAndQuit"] = "R\u00e9initialiser && Quitter",
            ["Quit"] = "Quitter",
        }
    };

    public static void SetCulture(CultureInfo culture)
    {
        _culture = culture;
    }

    public static string Get(string key)
    {
        var lang = _culture.TwoLetterISOLanguageName;

        if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;

        // Fallback to English
        if (Translations["en"].TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }
}
