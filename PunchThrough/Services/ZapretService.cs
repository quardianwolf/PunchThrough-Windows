using System.Diagnostics;
using System.IO;
using System.Reflection;
using PunchThrough.Models;

namespace PunchThrough.Services;

/// <summary>
/// Manages the zapret (winws.exe) process for packet-level DPI bypass.
/// No proxy — works at the Windows network stack level via WinDivert.
/// </summary>
public class ZapretService : IDisposable
{
    public const string DefaultStrategy = "--dpi-desync=split --dpi-desync-split-pos=1 --dpi-desync-fooling=md5sig";

    private Process? _process;

    private static string BinDirectory => AppPaths.ZapretDir;
    public static string WinwsPath => Path.Combine(BinDirectory, "winws.exe");
    public static string HostlistPath => Path.Combine(BinDirectory, "hostlist.txt");

    /// <summary>
    /// Extract zapret binaries from embedded resources. Files that differ from the bundled
    /// version (app update) are replaced; files in use by a running winws are left alone.
    /// </summary>
    public static void EnsureExtracted()
    {
        Directory.CreateDirectory(BinDirectory);

        var files = new[]
        {
            ("PunchThrough.Assets.winws.exe", "winws.exe"),
            ("PunchThrough.Assets.cygwin1.dll", "cygwin1.dll"),
            ("PunchThrough.Assets.WinDivert.dll", "WinDivert.dll"),
            ("PunchThrough.Assets.WinDivert64.sys", "WinDivert64.sys"),
        };

        var assembly = Assembly.GetExecutingAssembly();

        foreach (var (resourceName, fileName) in files)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            var targetPath = Path.Combine(BinDirectory, fileName);
            var existing = new FileInfo(targetPath);
            if (existing.Exists && existing.Length == stream.Length) continue;

            try
            {
                using var fileStream = File.Create(targetPath);
                stream.CopyTo(fileStream);
            }
            catch (IOException) when (existing.Exists)
            {
                // Locked by a loaded driver / running process — keep the old copy for this session
            }
            catch (UnauthorizedAccessException) when (existing.Exists)
            {
            }
        }
    }

    /// <summary>
    /// Start zapret with the appropriate mode. Throws when winws does not stay alive.
    /// </summary>
    public void Start(ProxyMode mode, string[] customDomains, string? customStrategy, bool quic)
    {
        Stop();
        EnsureExtracted();

        if (!File.Exists(WinwsPath))
            throw new FileNotFoundException(Strings.Get("ErrEngineMissing"));

        var hostlist = mode == ProxyMode.Full ? null : WriteHostlist(mode, customDomains);
        var args = BuildArguments(customStrategy, hostlist, quic);
        Debug.WriteLine($"[Zapret] Starting: winws.exe {args}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = WinwsPath,
                Arguments = args,
                WorkingDirectory = BinDirectory,
                // UseShellExecute=true with hidden window mimics PowerShell's Start-Process,
                // which gives winws its own console — required for cygwin-based winws to work
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        process.Start();
        _process = process;

        // winws quits within a moment when the driver cannot load or an argument is rejected.
        // Without this check the UI would happily report "Connected" with nothing running.
        if (process.WaitForExit(1500) && !AnyWinwsProcess())
        {
            var code = process.ExitCode;
            _process = null;
            throw new InvalidOperationException(Strings.Format("ErrEngineExited", code));
        }

        Debug.WriteLine($"[Zapret] Started PID: {process.Id}");
    }

    public void Stop()
    {
        var process = _process;
        _process = null;

        if (process is { HasExited: false })
        {
            try { process.Kill(); } catch { }
        }
        process?.Dispose();

        KillAll();
    }

    public static void KillAll()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("winws"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
                proc.Dispose();
            }
        }
        catch { }
    }

    public bool IsRunning
    {
        get
        {
            try { if (_process is { HasExited: false }) return true; }
            catch { }
            // The launcher handle is not the whole truth for a cygwin binary — trust the process list
            return AnyWinwsProcess();
        }
    }

    private static bool AnyWinwsProcess()
    {
        try
        {
            var processes = Process.GetProcessesByName("winws");
            foreach (var p in processes) p.Dispose();
            return processes.Length > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Build the winws command line. With <paramref name="quic"/> a second profile handles
    /// QUIC (HTTP/3 over UDP 443), which browsers prefer and DPI blocks separately.
    /// </summary>
    public static string BuildArguments(string? strategy, string? hostlistPath, bool quic)
    {
        if (string.IsNullOrWhiteSpace(strategy)) strategy = DefaultStrategy;
        var hostlist = hostlistPath == null ? "" : $" --hostlist=\"{hostlistPath}\"";

        if (!quic)
            return $"--wf-tcp=443 --filter-tcp=443{hostlist} {strategy}";

        return "--wf-tcp=443 --wf-udp=443 " +
               $"--filter-udp=443{hostlist} --dpi-desync=fake --dpi-desync-repeats=6 --new " +
               $"--filter-tcp=443{hostlist} {strategy}";
    }

    private static string WriteHostlist(ProxyMode mode, string[] customDomains)
    {
        var baseDomains = mode == ProxyMode.DiscordOnly ? KnownDomains.DiscordDomains : customDomains;

        // zapret matches subdomains automatically, so bare domains are enough
        var domains = baseDomains
            .Select(DomainUtil.Normalize)
            .Where(d => d != null)
            .Distinct()
            .ToList();

        // zapret treats an empty list as "no list" and would then touch ALL traffic —
        // the opposite of what a selective mode with nothing selected should do.
        if (domains.Count == 0)
            domains.Add("punchthrough.invalid");

        File.WriteAllLines(HostlistPath, domains!);
        return HostlistPath;
    }

    public void Dispose() => Stop();
}
