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
    private Process? _process;

    private static string BinDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PunchThrough", "zapret");

    private static string WinwsPath => Path.Combine(BinDirectory, "winws.exe");
    private static string HostlistPath => Path.Combine(BinDirectory, "hostlist.txt");

    /// <summary>
    /// Extract zapret binaries from embedded resources if needed.
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
            var targetPath = Path.Combine(BinDirectory, fileName);
            if (File.Exists(targetPath)) continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }

    /// <summary>
    /// Start zapret with the appropriate mode.
    /// </summary>
    public void Start(ProxyMode mode, string[] customDomains, string? customStrategy = null)
    {
        Stop();
        EnsureExtracted();

        var args = BuildArguments(mode, customDomains, customStrategy);
        Debug.WriteLine($"[Zapret] Starting: winws.exe {args}");

        _process = new Process
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
            },
            EnableRaisingEvents = true
        };

        _process.Start();

        Debug.WriteLine($"[Zapret] Started PID: {_process.Id}");
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(); } catch { }
        }

        // Kill all winws instances
        try
        {
            foreach (var proc in Process.GetProcessesByName("winws"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }

        _process = null;
    }

    public bool IsRunning => _process is { HasExited: false };

    private string BuildArguments(ProxyMode mode, string[] customDomains, string? customStrategy)
    {
        // Use detected/custom strategy if available, otherwise default
        var strategy = !string.IsNullOrWhiteSpace(customStrategy)
            ? customStrategy
            : "--dpi-desync=split --dpi-desync-split-pos=1 --dpi-desync-fooling=md5sig";

        var args = $"--wf-tcp=443 --filter-tcp=443 {strategy}";

        if (mode != ProxyMode.Full)
        {
            // Write hostlist file for selective bypass
            var baseDomains = mode == ProxyMode.DiscordOnly
                ? KnownDomains.DiscordDomains
                : customDomains;

            // Add www. variants
            var allDomains = new List<string>();
            foreach (var d in baseDomains)
            {
                allDomains.Add(d);
                if (!d.StartsWith("www."))
                    allDomains.Add($"www.{d}");
            }

            File.WriteAllLines(HostlistPath, allDomains);
            args += $" --hostlist=\"{HostlistPath}\"";
        }

        return args;
    }

    public void Dispose() => Stop();
}
