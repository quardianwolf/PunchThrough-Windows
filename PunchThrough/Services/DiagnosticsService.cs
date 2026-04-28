using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PunchThrough.Services;

/// <summary>
/// Collects comprehensive diagnostics and saves to desktop log.
/// </summary>
public static class DiagnosticsService
{
    private static readonly HttpClient DohClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<string> RunDiagnosticsAsync()
    {
        var log = new StringBuilder();
        log.AppendLine($"=== PunchThrough Diagnostics — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        log.AppendLine($"OS: {Environment.OSVersion.VersionString}");
        log.AppendLine($"Machine: {Environment.MachineName}");
        log.AppendLine();

        // System DNS
        log.AppendLine("--- System DNS ---");
        log.AppendLine(RunPs("(Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1 | Get-NetAdapter).Name"));
        log.AppendLine(RunPs("Get-DnsClientServerAddress -AddressFamily IPv4 | Where-Object ServerAddresses -ne $null | Select-Object InterfaceAlias, ServerAddresses | Format-List"));
        log.AppendLine(RunPs("Get-DnsClientDohServerAddress -ServerAddress 1.1.1.1, 1.0.0.1 | Format-List"));
        log.AppendLine();

        // DNS resolution test
        log.AppendLine("--- DNS Resolution ---");
        log.AppendLine(RunPs("Resolve-DnsName discord.com -Type A -ErrorAction SilentlyContinue | Format-List"));
        log.AppendLine(RunPs("Resolve-DnsName updates.discord.com -Type A -ErrorAction SilentlyContinue | Format-List"));
        log.AppendLine();

        // zapret status
        log.AppendLine("--- Zapret (winws) ---");
        log.AppendLine(RunPs("Get-Process winws -ErrorAction SilentlyContinue | Format-List Id, StartTime"));
        log.AppendLine(RunPs("Get-CimInstance Win32_Process -Filter \"name='winws.exe'\" | Select-Object CommandLine | Format-List"));
        log.AppendLine();

        // Hostlist
        log.AppendLine("--- Hostlist ---");
        try
        {
            var hostlistPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PunchThrough", "zapret", "hostlist.txt");
            if (File.Exists(hostlistPath))
                log.AppendLine(File.ReadAllText(hostlistPath));
            else
                log.AppendLine("(no hostlist — Full Bypass mode)");
        }
        catch (Exception ex) { log.AppendLine($"Error: {ex.Message}"); }
        log.AppendLine();

        // Settings
        log.AppendLine("--- Settings ---");
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PunchThrough", "settings.json");
            if (File.Exists(settingsPath))
                log.AppendLine(File.ReadAllText(settingsPath));
        }
        catch (Exception ex) { log.AppendLine($"Error: {ex.Message}"); }
        log.AppendLine();

        // DoH-resolved IP
        log.AppendLine("--- DoH Resolution Test ---");
        var dohIp = await ResolveViaDoH("discord.com");
        log.AppendLine($"discord.com via DoH: {dohIp ?? "FAILED"}");
        var updatesIp = await ResolveViaDoH("updates.discord.com");
        log.AppendLine($"updates.discord.com via DoH: {updatesIp ?? "FAILED"}");
        log.AppendLine();

        // Direct IP TLS test (bypasses system DNS)
        log.AppendLine("--- Direct IP TLS Tests ---");
        if (dohIp != null)
            log.AppendLine($"discord.com (direct): {await TestEndpoint("discord.com", dohIp)}");
        if (updatesIp != null)
            log.AppendLine($"updates.discord.com (direct): {await TestEndpoint("updates.discord.com", updatesIp)}");
        log.AppendLine();

        // System DNS TLS test (uses Windows DNS — should work if DoH is active)
        log.AppendLine("--- System DNS TLS Tests ---");
        log.AppendLine($"discord.com (system DNS): {await TestEndpointSystemDns("discord.com")}");
        log.AppendLine($"updates.discord.com (system DNS): {await TestEndpointSystemDns("updates.discord.com")}");
        log.AppendLine();

        // Save log
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"punchthrough_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var fullPath = Path.Combine(desktop, fileName);
        File.WriteAllText(fullPath, log.ToString());
        return fullPath;
    }

    private static string RunPs(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{command.Replace("\"", "`\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "(failed to start)";
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(error) ? output.Trim() : $"{output.Trim()}\n[stderr]: {error.Trim()}";
        }
        catch (Exception ex) { return $"(error: {ex.Message})"; }
    }

    private static async Task<string?> ResolveViaDoH(string host)
    {
        try
        {
            var url = $"https://cloudflare-dns.com/dns-query?name={host}&type=A";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/dns-json");
            var resp = await DohClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Answer", out var answers)) return null;
            foreach (var a in answers.EnumerateArray())
                if (a.GetProperty("type").GetInt32() == 1)
                    return a.GetProperty("data").GetString();
            return null;
        }
        catch (Exception ex) { return $"error: {ex.Message}"; }
    }

    private static async Task<string> TestEndpoint(string host, string ip)
    {
        try
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, ct) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await socket.ConnectAsync(IPAddress.Parse(ip), 443, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await client.GetAsync($"https://{host}/", HttpCompletionOption.ResponseHeadersRead);
            return $"OK (HTTP {(int)resp.StatusCode}, IP {ip})";
        }
        catch (Exception ex)
        {
            return $"FAIL ({ex.InnerException?.Message ?? ex.Message})";
        }
    }

    private static async Task<string> TestEndpointSystemDns(string host)
    {
        // Capture which IP system DNS actually picks
        string? actualIp = null;
        try
        {
            var dnsResult = await System.Net.Dns.GetHostAddressesAsync(host);
            actualIp = dnsResult.Length > 0 ? string.Join(",", dnsResult.Select(a => a.ToString())) : "(none)";
        }
        catch (Exception dnsEx)
        {
            actualIp = $"(DNS error: {dnsEx.Message})";
        }

        try
        {
            using var client = new HttpClient(new SocketsHttpHandler())
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var resp = await client.GetAsync($"https://{host}/", HttpCompletionOption.ResponseHeadersRead);
            return $"OK (HTTP {(int)resp.StatusCode}) [system DNS returned: {actualIp}]";
        }
        catch (Exception ex)
        {
            return $"FAIL ({ex.InnerException?.Message ?? ex.Message}) [system DNS returned: {actualIp}]";
        }
    }
}
