using System.IO;
using System.Text.Json;

namespace PunchThrough.Models;

public class Settings
{
    public DnsServer DnsServer { get; set; } = DnsServer.Cloudflare;
    public string CustomDns { get; set; } = "";
    public bool EnableDoH { get; set; } = true;
    public bool EnableQuic { get; set; }
    public bool LaunchAtStartup { get; set; }
    public bool AutoConnect { get; set; }
    public ProxyMode ProxyMode { get; set; } = ProxyMode.Full;
    public List<string> CustomProxyDomains { get; set; } = new();
    public string ZapretStrategy { get; set; } = ""; // empty = use default
    public AppLanguage AppLanguage { get; set; } = AppLanguage.System;

    // SSH tunnel
    public TunnelMode TunnelMode { get; set; } = TunnelMode.Off;
    public string TunnelHost { get; set; } = "";
    public int TunnelPort { get; set; } = 22;
    public string TunnelUser { get; set; } = "";
    public string TunnelKeyPath { get; set; } = "";
    public List<string> TunnelDomains { get; set; } = new();

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PunchThrough");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                settings.CustomProxyDomains ??= new();
                settings.TunnelDomains ??= new();
                settings.CustomDns ??= "";
                settings.ZapretStrategy ??= "";
                settings.TunnelHost ??= "";
                settings.TunnelUser ??= "";
                settings.TunnelKeyPath ??= "";
                return settings;
            }
        }
        catch
        {
            // Return defaults on any error
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail — settings are not critical
        }
    }
}
