using System.Diagnostics;
using System.IO;

namespace PunchThrough.Services;

public static class AppPaths
{
    /// <summary>%LocalAppData%\PunchThrough — settings, zapret binaries, state backups.</summary>
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PunchThrough");

    public static string ZapretDir => Path.Combine(DataDir, "zapret");
}

internal static class ProcessUtil
{
    /// <summary>Run a console tool hidden and wait for it. Returns (exit code, stdout).</summary>
    public static (int exitCode, string output) Run(string fileName, string arguments, int timeoutMs = 15000)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (-1, "");

            // Drain both pipes asynchronously so a chatty tool can never block on a full buffer
            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (-1, "");
            }
            stderr.Wait(1000);
            return (proc.ExitCode, stdout.Wait(1000) ? stdout.Result : "");
        }
        catch
        {
            return (-1, "");
        }
    }

    /// <summary>Run a PowerShell script without touching the disk.</summary>
    public static (int exitCode, string output) RunPowerShell(string script, int timeoutMs = 20000)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        return Run("powershell", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeoutMs);
    }
}
