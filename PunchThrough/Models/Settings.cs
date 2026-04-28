using System.IO;
using System.Text.Json;

namespace PunchThrough.Models;

public class Settings
{
    public DnsServer DnsServer { get; set; } = DnsServer.Cloudflare;
    public string CustomDns { get; set; } = "";
    public int SpoofDpiPort { get; set; } = 8080;
    public bool EnableDoH { get; set; } = true;
    public bool EnableSystemProxy { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
    public bool AutoConnect { get; set; }
    public ProxyMode ProxyMode { get; set; } = ProxyMode.Full;
    public List<string> CustomProxyDomains { get; set; } = new();
    public string ZapretStrategy { get; set; } = ""; // empty = use default
    public AppLanguage AppLanguage { get; set; } = AppLanguage.System;

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
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
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
