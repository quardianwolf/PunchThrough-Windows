using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using PunchThrough.Models;

namespace PunchThrough.Services;

/// <summary>
/// Tests multiple zapret strategies to find one that works on the user's ISP.
/// </summary>
public static class StrategyDetector
{
    public const string DefaultTestHost = "discord.com";
    private const int MaxTargets = 6;

    /// <summary>
    /// Strategies sorted by effectiveness on Turkish ISPs (most likely first). First working one wins.
    /// Each is a zapret command-line argument string, tested for ~3 seconds.
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
    /// The hosts a strategy has to unblock for the given mode: the user's own sites in
    /// Custom mode (a strategy that opens Discord does not necessarily open them), Discord otherwise.
    /// </summary>
    public static string[] TestHostsFor(ProxyMode mode, IEnumerable<string> customDomains)
    {
        if (mode == ProxyMode.Custom)
        {
            var custom = customDomains.Select(DomainUtil.Normalize).Where(d => d != null).Take(2).ToArray();
            if (custom.Length > 0) return custom!;
        }
        return new[] { DefaultTestHost };
    }

    /// <summary>
    /// Returns the args of the first strategy that connects to every test target.
    /// Returns null if nothing works.
    /// </summary>
    public static async Task<(string? args, string? name)> DetectAsync(
        string[]? testHosts = null,
        IProgress<(int current, int total, string strategy)>? progress = null)
    {
        testHosts = testHosts is { Length: > 0 } ? testHosts : new[] { DefaultTestHost };

        // Make sure no winws is running
        ZapretService.KillAll();
        await Task.Delay(500);

        ZapretService.EnsureExtracted();

        var log = new System.Text.StringBuilder();
        log.AppendLine($"PunchThrough Strategy Detection — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine($"OS: {Environment.OSVersion.VersionString}");
        log.AppendLine($"Test hosts: {string.Join(", ", testHosts)}");
        log.AppendLine($"Total strategies: {Strategies.Length}");

        // Resolve via DoH (CDNs load-balance; some IPs may be blocked while others are not)
        var targets = new List<(string host, string ip)>();
        foreach (var host in testHosts)
        {
            var ips = await DohResolver.ResolveAllAsync(host);
            log.AppendLine($"Resolved {host} via DoH: {(ips.Count > 0 ? string.Join(", ", ips) : "FAILED")}");
            targets.AddRange(ips.Select(ip => (host, ip)));
        }
        targets = targets.Take(MaxTargets).ToList();
        log.AppendLine($"Strategy must work on ALL {targets.Count} targets to be selected");
        log.AppendLine();

        if (targets.Count == 0)
        {
            log.AppendLine("Cannot resolve any test domain via DoH. Detection aborted.");
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
                proc = StartZapret(strategyArgs);
                if (proc == null)
                {
                    log.AppendLine($"[{i + 1}/{Strategies.Length}] {name} — FAILED TO START");
                    continue;
                }

                await Task.Delay(800);

                // All targets at once — a strategy costs ~3s no matter how many targets there are
                var outcomes = await Task.WhenAll(targets.Select(t => TestEndpoint(t.host, t.ip, 3)));
                var allSucceeded = outcomes.All(o => o.ok);
                var results = targets.Zip(outcomes, (t, o) => $"{t.host}@{t.ip}={(o.ok ? "OK" : "FAIL")}");

                log.AppendLine($"[{i + 1}/{Strategies.Length}] {name} — {(allSucceeded ? "SUCCESS" : "PARTIAL/FAIL")} [{string.Join(", ", results)}]");

                if (allSucceeded)
                {
                    log.AppendLine();
                    log.AppendLine($"WINNER: {name}");
                    log.AppendLine($"ARGS: {strategyArgs}");

                    if (testHosts.Contains(DefaultTestHost))
                    {
                        log.AppendLine();
                        log.AppendLine("=== EXTENDED DIAGNOSTICS ===");
                        foreach (var host in new[] { "updates.discord.com", "gateway.discord.gg", "cdn.discordapp.com" })
                        {
                            var ip = await DohResolver.ResolveAsync(host);
                            var (ok, detail) = ip == null
                                ? (false, "(DoH resolution failed)")
                                : await TestEndpoint(host, ip, 4);
                            log.AppendLine($"  {host}: {(ok ? "OK" : "FAIL")} {detail}");
                        }
                    }

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

    /// <summary>HTTPS request to <paramref name="host"/> pinned to one IP, bypassing system DNS.</summary>
    private static async Task<(bool ok, string detail)> TestEndpoint(string host, string ip, int timeoutSeconds)
    {
        try
        {
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, ct) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Parse(ip), 443, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            using var resp = await client.GetAsync($"https://{host}/", HttpCompletionOption.ResponseHeadersRead);
            // Any HTTP answer (even 403) means the TLS handshake got through the DPI
            return ((int)resp.StatusCode < 500, $"(HTTP {(int)resp.StatusCode}, IP {ip})");
        }
        catch (Exception ex)
        {
            return (false, $"({ex.InnerException?.Message ?? ex.Message})");
        }
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

    private static Process? StartZapret(string strategyArgs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ZapretService.WinwsPath,
                Arguments = ZapretService.BuildArguments(strategyArgs, hostlistPath: null, quic: false),
                WorkingDirectory = AppPaths.ZapretDir,
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
}
