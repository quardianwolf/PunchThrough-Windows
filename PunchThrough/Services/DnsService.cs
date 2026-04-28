using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace PunchThrough.Services;

/// <summary>
/// Manages DNS for DPI bypass.
/// - Full mode: Sets system DNS to Cloudflare DoH (covers all domains automatically)
/// - Discord/Custom mode: Resolves specific domains via DoH and adds to hosts file
/// </summary>
public static class DnsService
{
    private static readonly HttpClient Http = new();
    private static readonly string HostsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    private const string Marker = "# PunchThrough";

    private static string? _originalDns;
    private static string? _activeInterface;
    private static bool _dnsChanged;

    /// <summary>
    /// Set up DNS based on proxy mode.
    /// Full: system DoH DNS. Discord/Custom: hosts file entries.
    /// </summary>
    public static async Task SetupDns(Models.ProxyMode mode, string[] customDomains)
    {
        // Always use system DoH — safe for all apps including Discord
        EnableSystemDoH();
    }

    /// <summary>
    /// Clean up all DNS changes.
    /// </summary>
    public static void Cleanup()
    {
        if (_dnsChanged)
            RestoreSystemDns();
        CleanupHosts();
    }

    // --- System DoH DNS (Full mode) ---

    private static void EnableSystemDoH()
    {
        try
        {
            _activeInterface = GetActiveInterface();
            if (_activeInterface == null) return;

            _originalDns = GetCurrentDns(_activeInterface);

            var scriptPath = Path.Combine(Path.GetTempPath(), "pt_dns_enable.ps1");
            File.WriteAllText(scriptPath,
                $"$iface = '{_activeInterface.Replace("'", "''")}'\n" +
                // IPv4 DNS to Cloudflare
                "netsh interface ip set dns name=\"$iface\" static 1.1.1.1 primary\n" +
                "netsh interface ip add dns name=\"$iface\" 1.0.0.1 index=2\n" +
                // IPv6 DNS to Cloudflare (otherwise IPv6 queries get poisoned by ISP)
                "netsh interface ipv6 set dns name=\"$iface\" static 2606:4700:4700::1111 primary\n" +
                "netsh interface ipv6 add dns name=\"$iface\" 2606:4700:4700::1001 index=2\n" +
                // Enable DoH AutoUpgrade for all four (IPv4 + IPv6)
                "Set-DnsClientDohServerAddress -ServerAddress 1.1.1.1 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 1.0.0.1 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 2606:4700:4700::1111 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 2606:4700:4700::1001 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue\n" +
                "ipconfig /flushdns | Out-Null\n");

            RunElevatedScript(scriptPath);
            _dnsChanged = true;
            Debug.WriteLine($"[DNS] System DoH enabled on {_activeInterface}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DNS] EnableSystemDoH failed: {ex.Message}");
        }
    }

    private static void RestoreSystemDns()
    {
        try
        {
            if (_activeInterface == null) return;

            var restoreCmd = string.IsNullOrEmpty(_originalDns) || _originalDns == "DHCP"
                ? $"netsh interface ip set dns name=\"{_activeInterface}\" dhcp"
                : $"netsh interface ip set dns name=\"{_activeInterface}\" static {_originalDns} primary";

            var scriptPath = Path.Combine(Path.GetTempPath(), "pt_dns_restore.ps1");
            File.WriteAllText(scriptPath,
                "Set-DnsClientDohServerAddress -ServerAddress 1.1.1.1 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $false -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 1.0.0.1 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $false -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 2606:4700:4700::1111 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $false -ErrorAction SilentlyContinue\n" +
                "Set-DnsClientDohServerAddress -ServerAddress 2606:4700:4700::1001 -DohTemplate https://cloudflare-dns.com/dns-query -AllowFallbackToUdp $false -AutoUpgrade $false -ErrorAction SilentlyContinue\n" +
                $"{restoreCmd}\n" +
                $"netsh interface ipv6 set dns name=\"{_activeInterface}\" dhcp\n" +
                "ipconfig /flushdns | Out-Null\n");

            RunElevatedScript(scriptPath);
            _dnsChanged = false;
            _activeInterface = null;
            _originalDns = null;
            Debug.WriteLine("[DNS] System DNS restored");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DNS] RestoreSystemDns failed: {ex.Message}");
        }
    }

    // --- Hosts file DNS (Discord/Custom mode) ---

    private static async Task AddDoHEntries(string[] domains)
    {
        var entries = new List<string>();

        foreach (var domain in domains)
        {
            try
            {
                var ip = await ResolveViaDoH(domain);
                if (ip != null)
                {
                    entries.Add($"{ip} {domain} {Marker}");
                    if (!domain.StartsWith("www."))
                        entries.Add($"{ip} www.{domain} {Marker}");
                    Debug.WriteLine($"[DNS] {domain} -> {ip}");
                }

                // Always try common subdomains
                foreach (var sub in new[] { "www", "cdn", "img", "ei", "di", "ci", "api", "static", "dl", "m" })
                {
                    var subDomain = $"{sub}.{domain}";
                    var subIp = await ResolveViaDoH(subDomain);
                    if (subIp != null)
                    {
                        entries.Add($"{subIp} {subDomain} {Marker}");
                        Debug.WriteLine($"[DNS] {subDomain} -> {subIp}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNS] Failed: {domain}: {ex.Message}");
            }
        }

        if (entries.Count == 0) return;

        try
        {
            var lines = File.Exists(HostsPath)
                ? new List<string>(File.ReadAllLines(HostsPath))
                : new List<string>();

            lines.RemoveAll(l => l.Contains(Marker));
            lines.AddRange(entries);
            File.WriteAllLines(HostsPath, lines);
            FlushDns();
            Debug.WriteLine($"[DNS] Added {entries.Count} hosts entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DNS] Hosts write failed: {ex.Message}");
        }
    }

    public static void CleanupHosts()
    {
        try
        {
            if (!File.Exists(HostsPath)) return;
            var lines = new List<string>(File.ReadAllLines(HostsPath));
            var before = lines.Count;
            lines.RemoveAll(l => l.Contains(Marker));
            if (lines.Count != before)
            {
                File.WriteAllLines(HostsPath, lines);
                FlushDns();
                Debug.WriteLine($"[DNS] Cleaned {before - lines.Count} hosts entries");
            }
        }
        catch { }
    }

    // --- Helpers ---

    private static async Task<string?> ResolveViaDoH(string domain)
    {
        var url = $"https://cloudflare-dns.com/dns-query?name={domain}&type=A";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/dns-json");

        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Answer", out var answers)) return null;

        foreach (var answer in answers.EnumerateArray())
            if (answer.GetProperty("type").GetInt32() == 1)
                return answer.GetProperty("data").GetString();

        return null;
    }

    public static string[] GetDomainsForMode(Models.ProxyMode mode, string[] customDomains)
    {
        var domains = new List<string>();
        switch (mode)
        {
            case Models.ProxyMode.DiscordOnly:
                domains.AddRange(Models.KnownDomains.DiscordDomains);
                break;
            case Models.ProxyMode.Custom:
                domains.AddRange(customDomains);
                break;
        }
        return domains.Distinct().ToArray();
    }

    private static void FlushDns()
    {
        var psi = new ProcessStartInfo("ipconfig", "/flushdns") { UseShellExecute = false, CreateNoWindow = true };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(3000);
    }

    private static void RunElevatedScript(string scriptPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                Verb = "runas"
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(15000);
        }
        catch { }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }

    private static string? GetActiveInterface()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"(Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1 | Get-NetAdapter).Name\"",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var name = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch { return null; }
    }

    private static string GetCurrentDns(string iface)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"$dns = (Get-DnsClientServerAddress -InterfaceAlias '{iface}' -AddressFamily IPv4).ServerAddresses; if ($dns.Count -eq 0) {{ 'DHCP' }} else {{ $dns[0] }}\"",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "DHCP";
            var result = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);
            return string.IsNullOrEmpty(result) ? "DHCP" : result;
        }
        catch { return "DHCP"; }
    }
}
