using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using PunchThrough.Models;

namespace PunchThrough.Services;

public sealed record TunnelConfig(
    TunnelMode Mode, string Host, int Port, string User, string KeyPath, IReadOnlyList<string> Domains)
{
    private static readonly Regex HostPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._:-]*$");
    private static readonly Regex UserPattern = new(@"^[A-Za-z0-9_][A-Za-z0-9._-]*$");

    public static TunnelConfig From(AppState s) =>
        new(s.TunnelMode, s.TunnelHost.Trim(), s.TunnelPort, s.TunnelUser.Trim(), s.TunnelKeyPath.Trim(), s.TunnelDomains.ToArray());

    /// <summary>Returns a user-facing problem description, or null when the server details are usable.</summary>
    public string? Validate()
    {
        // These values end up on the ssh command line, so anything that is not a plain
        // host / user name is rejected rather than quoted.
        if (!HostPattern.IsMatch(Host)) return Strings.Get("TunnelErrHost");
        if (Port is < 1 or > 65535) return Strings.Get("TunnelErrPort");
        if (!UserPattern.IsMatch(User)) return Strings.Get("TunnelErrUser");
        if (string.IsNullOrWhiteSpace(KeyPath) || !File.Exists(KeyPath)) return Strings.Get("TunnelErrKeyMissing");

        try
        {
            using var reader = new StreamReader(KeyPath);
            var first = reader.ReadLine() ?? "";
            if (first.StartsWith("PuTTY-User-Key-File")) return Strings.Get("TunnelErrKeyPpk");
            if (first.StartsWith("ssh-") || first.StartsWith("ecdsa-")) return Strings.Get("TunnelErrKeyPublic");
        }
        catch
        {
            return Strings.Get("TunnelErrKeyMissing");
        }

        return null;
    }
}

public enum TunnelCheck { Client, Reach, Login, Exit }

/// <summary>
/// A lightweight "VPN" built from things every machine already has: the OpenSSH client opens
/// a SOCKS proxy through the user's own server (ssh -D), and a proxy auto-config script tells
/// browsers which sites should use it. Nothing needs to be installed on the server.
/// </summary>
public sealed class TunnelService : IDisposable
{
    private readonly object _gate = new();
    private Process? _ssh;
    private HttpListener? _pacServer;
    private CancellationTokenSource? _cts;
    private int _socksPort;

    /// <summary>Raised when the tunnel dropped and could not be re-established.</summary>
    public event Action<string>? Lost;
    public event Action<string, LogLevel>? Log;

    public bool IsRunning
    {
        get
        {
            try { return _ssh is { HasExited: false }; }
            catch { return false; }
        }
    }

    // --- SSH client ---

    public static string? FindSsh()
    {
        var builtIn = Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh.exe");
        if (File.Exists(builtIn)) return builtIn;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), "ssh.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    /// <summary>Install Windows' optional "OpenSSH Client" feature. Can take a few minutes.</summary>
    public static Task<bool> InstallSshAsync() => Task.Run(() =>
    {
        ProcessUtil.RunPowerShell(
            "$c = Get-WindowsCapability -Online -Name 'OpenSSH.Client*' | Select-Object -First 1\n" +
            "if ($c -and $c.State -ne 'Installed') { Add-WindowsCapability -Online -Name $c.Name | Out-Null }",
            timeoutMs: 10 * 60 * 1000);
        return FindSsh() != null;
    });

    // --- Lifecycle ---

    /// <summary>Open the tunnel and point the system proxy script at it. Throws with a readable message.</summary>
    public async Task StartAsync(TunnelConfig config)
    {
        Stop();

        var problem = config.Validate();
        if (problem != null) throw new InvalidOperationException(problem);

        var ssh = FindSsh() ?? throw new InvalidOperationException(Strings.Get("TunnelSshMissing"));
        var key = PrepareKey(config.KeyPath);

        var cts = new CancellationTokenSource();
        var socksPort = FreePort();
        var process = await LaunchAsync(ssh, config, key, socksPort, cts.Token);

        lock (_gate)
        {
            _cts = cts;
            _ssh = process;
            _socksPort = socksPort;
        }

        try
        {
            var pacPort = FreePort();
            StartPacServer(pacPort, BuildPac(config, socksPort));
            // The query string defeats PAC caching when the domain list changed
            SystemProxy.SetPac($"http://127.0.0.1:{pacPort}/proxy.pac?v={DateTime.UtcNow.Ticks}");
        }
        catch
        {
            Stop();
            throw;
        }

        _ = MonitorAsync(ssh, config, key, cts.Token);
    }

    public void Stop()
    {
        Process? process;
        HttpListener? pac;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            process = _ssh; _ssh = null;
            pac = _pacServer; _pacServer = null;
            cts = _cts; _cts = null;
        }

        try { cts?.Cancel(); } catch { }
        Kill(process);
        try { pac?.Close(); } catch { }
        SystemProxy.Restore();
    }

    /// <summary>Public IP and country the tunnel exits from.</summary>
    public Task<(string ip, string country)> QueryExitAsync() => QueryExitAsync(_socksPort, CancellationToken.None);

    /// <summary>Restart ssh when it dies (server reboot, Wi-Fi change, sleep/resume).</summary>
    private async Task MonitorAsync(string ssh, TunnelConfig config, string key, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Process? current;
            lock (_gate) current = _ssh;
            if (current == null) return;

            try { await current.WaitForExitAsync(ct); }
            catch (OperationCanceledException) { return; }

            Log?.Invoke(Strings.Get("TunnelLogDropped"), LogLevel.Warning);

            Process? replacement = null;
            var lastError = "";
            for (var attempt = 1; attempt <= 6 && replacement == null; attempt++)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(3 * attempt, 15)), ct);
                    replacement = await LaunchAsync(ssh, config, key, _socksPort, ct);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { lastError = ex.Message; }
            }

            if (replacement == null)
            {
                // Take the proxy script away, otherwise the selected sites (or everything,
                // in all-traffic mode) would keep pointing at a dead local port.
                Stop();
                Lost?.Invoke(lastError);
                return;
            }

            lock (_gate)
            {
                if (ct.IsCancellationRequested) { Kill(replacement); return; }
                current.Dispose();
                _ssh = replacement;
            }
            Log?.Invoke(Strings.Get("TunnelLogRestored"), LogLevel.Info);
        }
    }

    // --- Guided connection test ---

    /// <summary>
    /// Walk through the checks in order and report each outcome, stopping at the first failure.
    /// Uses its own temporary tunnel, so it never disturbs a running one.
    /// </summary>
    public static async Task<bool> TestAsync(
        TunnelConfig config, Action<TunnelCheck, bool, string> report, CancellationToken ct = default)
    {
        var ssh = FindSsh();
        if (ssh == null)
        {
            report(TunnelCheck.Client, false, Strings.Get("TunnelSshMissing"));
            return false;
        }
        report(TunnelCheck.Client, true, Strings.Get("TunnelSshFound"));

        var problem = config.Validate();
        if (problem != null)
        {
            report(TunnelCheck.Reach, false, problem);
            return false;
        }

        if (!await CanConnectAsync(config.Host, config.Port, 8000))
        {
            report(TunnelCheck.Reach, false, Strings.Format("TunnelReachFail", config.Host, config.Port));
            return false;
        }
        report(TunnelCheck.Reach, true, Strings.Format("TunnelReachOk", config.Host, config.Port));

        Process? process = null;
        try
        {
            var socksPort = FreePort();
            try
            {
                var key = await Task.Run(() => PrepareKey(config.KeyPath, "id_test"));
                process = await LaunchAsync(ssh, config, key, socksPort, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report(TunnelCheck.Login, false, Strings.Format("TunnelLoginFail", ex.Message));
                return false;
            }
            report(TunnelCheck.Login, true, Strings.Format("TunnelLoginOk", config.User));

            try
            {
                var (ip, country) = await QueryExitAsync(socksPort, ct);
                report(TunnelCheck.Exit, true, Strings.Format("TunnelExitOk", ip, country));
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report(TunnelCheck.Exit, false, Strings.Format("TunnelExitFail", ex.InnerException?.Message ?? ex.Message));
                return false;
            }
        }
        finally
        {
            Kill(process);
        }
    }

    // --- ssh process ---

    private static async Task<Process> LaunchAsync(
        string ssh, TunnelConfig config, string keyPath, int socksPort, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ssh)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[]
        {
            "-N", "-T",
            "-D", $"127.0.0.1:{socksPort}",
            "-p", config.Port.ToString(),
            "-i", keyPath,
            "-o", "BatchMode=yes",                    // never wait for a password prompt nobody can see
            "-o", "IdentitiesOnly=yes",
            "-o", "ExitOnForwardFailure=yes",
            "-o", "StrictHostKeyChecking=accept-new", // trust on first use, refuse if the key later changes
            "-o", "ConnectTimeout=10",
            "-o", "ServerAliveInterval=20",
            "-o", "ServerAliveCountMax=3",
            $"{config.User}@{config.Host}"
        })
            psi.ArgumentList.Add(arg);

        var errors = new List<string>();
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data) || e.Data.StartsWith("Warning: Permanently added")) return;
            lock (errors)
            {
                errors.Add(e.Data.Trim());
                if (errors.Count > 10) errors.RemoveAt(0);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        try
        {
            // ssh opens the local SOCKS port only after it has authenticated,
            // so "the port accepts connections" means "we are logged in".
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (process.HasExited)
                {
                    await Task.Delay(200, ct); // let the last stderr lines arrive
                    string message;
                    lock (errors) message = string.Join(" ", errors.TakeLast(2));
                    throw new InvalidOperationException(
                        string.IsNullOrEmpty(message) ? Strings.Format("TunnelErrSshExit", process.ExitCode) : message);
                }

                if (await CanConnectAsync("127.0.0.1", socksPort, 1000)) return process;
                await Task.Delay(300, ct);
            }

            throw new TimeoutException(Strings.Get("TunnelErrTimeout"));
        }
        catch
        {
            Kill(process);
            throw;
        }
    }

    private static void Kill(Process? process)
    {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        try { process.Dispose(); } catch { }
    }

    /// <summary>
    /// OpenSSH refuses private keys that other accounts can read — the usual state of a key
    /// sitting in Downloads. Work on a private copy with a locked-down ACL instead of asking
    /// the user to fix file permissions by hand.
    /// </summary>
    private static string PrepareKey(string source, string name = "id_tunnel")
    {
        var dir = Path.Combine(AppPaths.DataDir, "tunnel");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, name);

        if (File.Exists(dest))
        {
            File.SetAttributes(dest, FileAttributes.Normal);
            File.Delete(dest);
        }
        File.Copy(source, dest);

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(dest).SetAccessControl(security);

        return dest;
    }

    // --- Proxy auto-config ---

    public static string BuildPac(TunnelConfig config, int socksPort)
    {
        var proxy = $"SOCKS5 127.0.0.1:{socksPort}; SOCKS 127.0.0.1:{socksPort}";
        var sb = new StringBuilder();
        sb.AppendLine("function FindProxyForURL(url, host) {");
        sb.AppendLine("  host = host.toLowerCase();");
        sb.AppendLine("  if (isPlainHostName(host) || host === 'localhost' || shExpMatch(host, '127.*') ||");
        sb.AppendLine("      shExpMatch(host, '10.*') || shExpMatch(host, '192.168.*') || shExpMatch(host, '169.254.*'))");
        sb.AppendLine("    return 'DIRECT';");

        if (config.Mode == TunnelMode.AllTraffic)
        {
            sb.AppendLine($"  return '{proxy}';");
        }
        else
        {
            var domains = config.Domains.Select(DomainUtil.Normalize).Where(d => d != null).Distinct();
            sb.AppendLine($"  var sites = [{string.Join(", ", domains.Select(d => $"'{d}'"))}];");
            sb.AppendLine("  for (var i = 0; i < sites.length; i++) {");
            sb.AppendLine("    if (host === sites[i] || dnsDomainIs(host, '.' + sites[i]))");
            sb.AppendLine($"      return '{proxy}';");
            sb.AppendLine("  }");
            sb.AppendLine("  return 'DIRECT';");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void StartPacServer(int port, string pac)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        lock (_gate) _pacServer = listener;

        var body = Encoding.ASCII.GetBytes(pac);
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    context.Response.ContentType = "application/x-ns-proxy-autoconfig";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body);
                    context.Response.Close();
                }
                catch
                {
                    try { if (!listener.IsListening) return; }
                    catch { return; }
                }
            }
        });
    }

    // --- Network helpers ---

    private static async Task<(string ip, string country)> QueryExitAsync(int socksPort, CancellationToken ct)
    {
        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"socks5://127.0.0.1:{socksPort}"),
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var text = await http.GetStringAsync("https://www.cloudflare.com/cdn-cgi/trace", ct);

        string Field(string name) =>
            text.Split('\n').FirstOrDefault(l => l.StartsWith(name + "="))?[(name.Length + 1)..].Trim() ?? "?";

        return (Field("ip"), Field("loc"));
    }

    private static async Task<bool> CanConnectAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose() => Stop();
}
