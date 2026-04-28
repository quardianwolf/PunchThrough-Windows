using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace PunchThrough.Services;

/// <summary>
/// Tests multiple zapret strategies to find one that works on the user's ISP.
/// </summary>
public static class StrategyDetector
{
    private static readonly HttpClient DohClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string TestHost = "discord.com";
    private const string TestUrl = "https://discord.com";

    private static List<string> _resolvedIps = new();

    /// <summary>
    /// Strategies to try in order. First working one wins.
    /// Each is a zapret command-line argument string.
    /// </summary>
    /// <summary>
    /// Strategies sorted by effectiveness on Turkish ISPs (most likely first).
    /// Each tested for ~3 seconds. Total max time = count * 3s.
    /// </summary>
    public static readonly (string Name, string Args)[] Strategies =
    {
        // Most reliable on TT / Vodafone / Turkcell
        ("split+md5sig",            "--dpi-desync=split --dpi-desync-split-pos=1 --dpi-desync-fooling=md5sig"),
        ("fake+ttl4",               "--dpi-desync=fake --dpi-desync-ttl=4"),
        ("fake+autottl",            "--dpi-desync=fake --dpi-desync-autottl=2 --dpi-desync-fooling=md5sig"),
        ("multidisorder+midsld",    "--dpi-desync=multidisorder --dpi-desync-split-pos=midsld --dpi-desync-fooling=md5sig --dpi-desync-repeats=6"),
        ("multidisorder+1,midsld",  "--dpi-desync=multidisorder --dpi-desync-split-pos=1,midsld --dpi-desync-fooling=md5sig --dpi-desync-repeats=6"),
        ("fake+fakedsplit",         "--dpi-desync=fake,fakedsplit --dpi-desync-autottl=2 --dpi-desync-fooling=md5sig"),
        ("fake+split (combo)",      "--dpi-desync=fake,split --dpi-desync-ttl=4 --dpi-desync-split-pos=1 --dpi-desync-fooling=md5sig"),

        // Mid-tier
        ("fake+badseq",             "--dpi-desync=fake --dpi-desync-fooling=badseq --dpi-desync-badseq-increment=0"),
        ("fake+ts",                 "--dpi-desync=fake --dpi-desync-fooling=ts"),
        ("fake+md5sig",             "--dpi-desync=fake --dpi-desync-fooling=md5sig"),
        ("hostfakesplit",           "--dpi-desync=hostfakesplit --dpi-desync-fooling=md5sig"),
        ("hostfakesplit+autottl",   "--dpi-desync=hostfakesplit --dpi-desync-autottl=2"),
        ("fakedsplit+md5sig",       "--dpi-desync=fakedsplit --dpi-desync-fooling=md5sig --dpi-desync-split-pos=1"),
        ("fakedsplit+badseq",       "--dpi-desync=fakedsplit --dpi-desync-fooling=badseq --dpi-desync-badseq-increment=0 --dpi-desync-split-pos=1"),
        ("fakedsplit+ts",           "--dpi-desync=fakedsplit --dpi-desync-fooling=ts --dpi-desync-split-pos=1"),
        ("fakeddisorder+md5sig",    "--dpi-desync=fakeddisorder --dpi-desync-fooling=md5sig --dpi-desync-split-pos=1"),
        ("fakeddisorder+badseq",    "--dpi-desync=fakeddisorder --dpi-desync-fooling=badseq --dpi-desync-badseq-increment=0 --dpi-desync-split-pos=1"),

        // Position variants
        ("split+sniext",            "--dpi-desync=split --dpi-desync-split-pos=sniext+1 --dpi-desync-fooling=md5sig"),
        ("multisplit+sniext",       "--dpi-desync=multisplit --dpi-desync-split-pos=sniext+1 --dpi-desync-fooling=md5sig"),
        ("multisplit+midsld",       "--dpi-desync=multisplit --dpi-desync-split-pos=midsld --dpi-desync-fooling=md5sig"),
        ("multisplit+1,midsld",     "--dpi-desync=multisplit --dpi-desync-split-pos=1,midsld --dpi-desync-fooling=md5sig"),

        // Aggressive autottl variants
        ("fake+autottl=-1",         "--dpi-desync=fake --dpi-desync-ttl=1 --dpi-desync-autottl=-1"),
        ("fake+autottl=-2",         "--dpi-desync=fake --dpi-desync-ttl=1 --dpi-desync-autottl=-2"),
        ("fake+autottl=-3",         "--dpi-desync=fake --dpi-desync-ttl=1 --dpi-desync-autottl=-3"),

        // Last resort with high repeats
        ("fake+repeats=11",         "--dpi-desync=fake --dpi-desync-ttl=4 --dpi-desync-repeats=11"),
        ("multidisorder+repeats11", "--dpi-desync=multidisorder --dpi-desync-split-pos=midsld --dpi-desync-fooling=md5sig --dpi-desync-repeats=11"),
    };

    /// <summary>
    /// Returns the args of the first strategy that successfully connects to Discord.
    /// Returns null if nothing works.
    /// </summary>
    public static async Task<(string? args, string? name)> DetectAsync(IProgress<(int current, int total, string strategy)>? progress = null)
    {
        // Make sure no winws is running
        KillAllWinws();
        await Task.Delay(500);

        ZapretService.EnsureExtracted();
        var winwsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PunchThrough", "zapret", "winws.exe");

        var log = new System.Text.StringBuilder();
        log.AppendLine($"PunchThrough Strategy Detection — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine($"OS: {Environment.OSVersion.VersionString}");
        log.AppendLine($"Tested URL: {TestUrl}");
        log.AppendLine($"Total strategies: {Strategies.Length}");

        // Resolve all IPs via DoH (Cloudflare load-balances; some IPs may be blocked)
        _resolvedIps = await ResolveAllIps(TestHost);
        log.AppendLine($"Resolved {TestHost} via DoH: {(_resolvedIps.Count > 0 ? string.Join(", ", _resolvedIps) : "FAILED")}");
        log.AppendLine($"Strategy must work on ALL {_resolvedIps.Count} IPs to be selected");
        log.AppendLine();

        if (_resolvedIps.Count == 0)
        {
            log.AppendLine("Cannot resolve test domain via DoH. Detection aborted.");
            SaveLog(log.ToString(), success: false);
            return (null, null);
        }

        for (int i = 0; i < Strategies.Length; i++)
        {
            var (name, strategyArgs) = Strategies[i];
            progress?.Report((i + 1, Strategies.Length, name));

            Process? proc = null;
            try
            {
                proc = StartZapret(winwsPath, strategyArgs);
                if (proc == null)
                {
                    log.AppendLine($"[{i + 1}/{Strategies.Length}] {name} — FAILED TO START");
                    continue;
                }

                await Task.Delay(800);

                // Test against ALL resolved IPs — strategy must work on all
                var results = new List<string>();
                bool allSucceeded = true;
                foreach (var ip in _resolvedIps)
                {
                    var (ok, err) = await TestConnectionWithError(ip);
                    results.Add($"{ip}={(ok ? "OK" : "FAIL")}");
                    if (!ok) allSucceeded = false;
                }

                log.AppendLine($"[{i + 1}/{Strategies.Length}] {name} — {(allSucceeded ? "SUCCESS" : "PARTIAL/FAIL")} [{string.Join(", ", results)}]");

                if (allSucceeded)
                {
                    log.AppendLine();
                    log.AppendLine($"WINNER: {name}");
                    log.AppendLine($"ARGS: {strategyArgs}");
                    log.AppendLine();
                    log.AppendLine("=== EXTENDED DIAGNOSTICS ===");

                    // Test other Discord endpoints with this strategy
                    foreach (var (host, label) in new[]
                    {
                        ("discord.com", "discord.com (main)"),
                        ("updates.discord.com", "updates.discord.com (updater)"),
                        ("gateway.discord.gg", "gateway.discord.gg (websocket)"),
                        ("cdn.discordapp.com", "cdn.discordapp.com (CDN)"),
                    })
                    {
                        var (ok, err) = await TestEndpointWithError(host);
                        log.AppendLine($"  {label}: {(ok ? "OK" : "FAIL")} {err}");
                    }

                    Stop(proc);
                    SaveLog(log.ToString(), success: true);
                    return (strategyArgs, name);
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"[{i + 1}/{Strategies.Length}] {name} — EXCEPTION: {ex.Message}");
                Debug.WriteLine($"[Detect] Strategy {name} failed: {ex.Message}");
            }
            finally
            {
                Stop(proc);
                await Task.Delay(200);
            }
        }

        log.AppendLine();
        log.AppendLine("NO STRATEGY WORKED — please share this log file");
        SaveLog(log.ToString(), success: false);
        return (null, null);
    }

    private static async Task<(bool success, string error)> TestConnectionWithError(string ip)
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

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var resp = await client.GetAsync(TestUrl, HttpCompletionOption.ResponseHeadersRead);
            return ((int)resp.StatusCode < 500, "");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return (false, $"({msg})");
        }
    }

    private static async Task<(bool ok, string err)> TestEndpointWithError(string host)
    {
        try
        {
            var ip = await ResolveViaDoH(host);
            if (ip == null) return (false, "(DoH resolution failed)");

            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, ct) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await socket.ConnectAsync(IPAddress.Parse(ip), 443, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
            using var resp = await client.GetAsync($"https://{host}/", HttpCompletionOption.ResponseHeadersRead);
            return ((int)resp.StatusCode < 500, $"(HTTP {(int)resp.StatusCode}, IP {ip})");
        }
        catch (Exception ex)
        {
            return (false, $"({ex.InnerException?.Message ?? ex.Message})");
        }
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
        catch { return null; }
    }

    private static async Task<List<string>> ResolveAllIps(string host)
    {
        var ips = new List<string>();
        try
        {
            var url = $"https://cloudflare-dns.com/dns-query?name={host}&type=A";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/dns-json");
            var resp = await DohClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return ips;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Answer", out var answers)) return ips;
            foreach (var a in answers.EnumerateArray())
                if (a.GetProperty("type").GetInt32() == 1)
                {
                    var ip = a.GetProperty("data").GetString();
                    if (!string.IsNullOrEmpty(ip)) ips.Add(ip);
                }
        }
        catch { }
        return ips;
    }

    private static async Task<string?> ResolveDiscordIp()
    {
        try
        {
            var url = $"https://cloudflare-dns.com/dns-query?name={TestHost}&type=A";
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
        catch { return null; }
    }

    private static void SaveLog(string content, bool success)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = success
                ? $"punchthrough_detection_success_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                : $"punchthrough_detection_FAILED_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            File.WriteAllText(Path.Combine(desktop, fileName), content);
        }
        catch { }
    }

    private static Process? StartZapret(string winwsPath, string strategyArgs)
    {
        try
        {
            var fullArgs = $"--wf-tcp=443 --filter-tcp=443 {strategyArgs}";
            var psi = new ProcessStartInfo
            {
                FileName = winwsPath,
                Arguments = fullArgs,
                WorkingDirectory = Path.GetDirectoryName(winwsPath)!,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return Process.Start(psi);
        }
        catch { return null; }
    }


    private static void Stop(Process? proc)
    {
        try { if (proc is { HasExited: false }) proc.Kill(); } catch { }
        try { proc?.Dispose(); } catch { }
    }

    private static void KillAllWinws()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("winws"))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
    }
}
