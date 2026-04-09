using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using PunchThrough.Models;

namespace PunchThrough.Services;

public class SpoofDPIService
{
    private Process? _currentProcess;

    private static readonly string LogFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pt_debug.log");

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:o}] {message}\n";
        try { System.IO.File.AppendAllText(LogFile, line); } catch { }
        Debug.WriteLine(line);
    }

    public async Task StartAsync(int port, string dnsAddress, bool enableDoH, bool enableSystemProxy, AppState appState)
    {
        Log("=== START ATTEMPT ===");

        // Find SpoofDPI binary — auto-download if missing
        var spoofDpiPath = FindSpoofDPI();
        if (spoofDpiPath == null)
        {
            Log("SpoofDPI not found, attempting auto-download...");
            appState.AddLog("SpoofDPI not found. Downloading...", LogLevel.Warning);

            var (ok, msg) = await SpoofDPIManager.DownloadAsync();
            if (!ok)
            {
                Log($"Auto-download failed: {msg}");
                throw new SpoofDPIException($"SpoofDPI not found and auto-download failed: {msg}");
            }

            spoofDpiPath = FindSpoofDPI();
            if (spoofDpiPath == null)
            {
                Log("ERROR: SpoofDPI still not found after download!");
                throw new SpoofDPIException("SpoofDPI download succeeded but binary not found.");
            }

            appState.AddLog($"SpoofDPI downloaded: {msg}");
        }

        Log($"Found spoofdpi at: {spoofDpiPath}");

        // Stop existing process
        await StopAsync(appState, disableProxy: false);

        // Kill all spoofdpi instances
        await KillAllSpoofDPI();

        // Check if port is in use
        if (IsPortInUse(port))
        {
            Log($"ERROR: Port {port} is still in use after cleanup!");
            throw new SpoofDPIException($"Port {port} is already in use. Close other applications using this port.");
        }

        // Build argument sets — try enhanced flags first, fall back to basic
        var enhancedArgs = BuildArguments(port, dnsAddress, enableDoH, includePolicy: true);
        var basicArgs = BuildArguments(port, dnsAddress, enableDoH, includePolicy: false);

        var argSets = new[] { ("enhanced", enhancedArgs), ("basic", basicArgs) };
        var started = false;

        foreach (var (label, arguments) in argSets)
        {
            Log($"Trying {label} flags: {spoofDpiPath} {string.Join(" ", arguments)}");

            var result = await LaunchProcess(spoofDpiPath, arguments, port);

            switch (result)
            {
                case LaunchResult.Success:
                    Log($"SUCCESS! SpoofDPI is running and listening on port {port} ({label} flags)");
                    started = true;
                    break;
                case LaunchResult.UnsupportedFlags:
                    Log($"Unsupported flags detected, falling back...");
                    continue;
                case LaunchResult.Failed:
                    throw new SpoofDPIException("Failed to start SpoofDPI");
            }

            if (started) break;
        }

        if (!started)
            throw new SpoofDPIException("Failed to start SpoofDPI");

        if (enableSystemProxy)
        {
            ProxyService.SetProxy(true, port);
            Log($"System proxy enabled on port {port}");
        }

        appState.AddLog($"SpoofDPI started on port {port}");
    }

    private static string[] BuildArguments(int port, string dnsAddress, bool enableDoH, bool includePolicy)
    {
        var args = new List<string>
        {
            "--listen-addr", $"127.0.0.1:{port}",
            "--dns-addr", $"{dnsAddress}:53",
            "--https-split-mode", "chunk",
            "--https-chunk-size", "5",
            "--silent"
        };

        if (enableDoH)
            args.AddRange(new[] { "--dns-mode", "https" });

        if (includePolicy)
            args.Add("--dns-cache");

        return args.ToArray();
    }

    private enum LaunchResult { Success, UnsupportedFlags, Failed }

    private async Task<LaunchResult> LaunchProcess(string binaryPath, string[] arguments, int port)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        var stderrOutput = "";
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderrOutput += e.Data + "\n";
        };

        try
        {
            Log("Starting process...");
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _currentProcess = process;
            Log($"Process started! PID: {process.Id}");
        }
        catch (Exception ex)
        {
            Log($"Process start FAILED: {ex.Message}");
            return LaunchResult.Failed;
        }

        // Wait for SpoofDPI to start listening
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (process.HasExited)
            {
                Log($"Process exited early with code: {process.ExitCode}");
                Log($"STDERR: {stderrOutput}");

                if (stderrOutput.Contains("flag provided but not defined"))
                {
                    _currentProcess = null;
                    return LaunchResult.UnsupportedFlags;
                }

                _currentProcess = null;
                return LaunchResult.Failed;
            }

            if (IsPortInUse(port))
            {
                Log($"Port {port} is listening after attempt {attempt}");

                var verified = await VerifyProxyConnection(port);
                Log(verified
                    ? "Proxy connection verified successfully"
                    : "WARNING: Port is listening but proxy connection test failed, proceeding anyway");

                return LaunchResult.Success;
            }

            Log($"Attempt {attempt}/{maxAttempts} - port not ready yet, waiting 500ms...");
            await Task.Delay(500);
        }

        Log($"ERROR: SpoofDPI process is running but not listening on port {port} after {maxAttempts} attempts");
        process.Kill();
        _currentProcess = null;
        return LaunchResult.Failed;
    }

    public async Task StopAsync(AppState appState, bool disableProxy = true)
    {
        Log("Stopping SpoofDPI...");

        if (_currentProcess is { HasExited: false })
        {
            try
            {
                _currentProcess.Kill();
                Log("Sent kill signal");
            }
            catch
            {
                // Process may have already exited
            }
        }

        await KillAllSpoofDPI();

        if (disableProxy)
        {
            ProxyService.SetProxy(false, 0);
            Log("System proxy disabled");
        }

        _currentProcess = null;
        appState.AddLog("SpoofDPI stopped");
    }

    private static async Task KillAllSpoofDPI()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("spoofdpi"))
            {
                try { process.Kill(); } catch { }
            }
        }
        catch
        {
            // Ignore errors
        }

        await Task.Delay(500);
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private static async Task<bool> VerifyProxyConnection(int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(3000));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public bool IsRunning() => _currentProcess is { HasExited: false };

    // Find SpoofDPI binary on Windows
    // Priority: 1) Our managed copy  2) Scoop/Choco  3) PATH
    public static string? FindSpoofDPI()
    {
        // 1. Our own managed binary (downloaded by SpoofDPIManager)
        if (SpoofDPIManager.IsInstalled())
            return SpoofDPIManager.BinaryPath;

        // 2. Common package manager installs
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "spoofdpi.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "spoofdpi.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spoofdpi.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // 3. Search PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir.Trim(), "spoofdpi.exe");
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public static bool IsSpoofDPIInstalled() => FindSpoofDPI() != null;

    /// <summary>
    /// Ensure SpoofDPI is available. If not found anywhere, auto-download it.
    /// </summary>
    public static async Task<(bool ok, string message)> EnsureInstalledAsync(IProgress<int>? progress = null)
    {
        if (IsSpoofDPIInstalled())
            return (true, $"SpoofDPI ready: {FindSpoofDPI()}");

        return await SpoofDPIManager.DownloadAsync(progress: progress);
    }
}

public class SpoofDPIException : Exception
{
    public SpoofDPIException(string message) : base(message) { }
}
