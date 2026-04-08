using System.Diagnostics;
using System.IO;

namespace PunchThrough.Services;

/// <summary>
/// Manages Windows Defender exclusions for SpoofDPI to prevent
/// Network Realtime Inspection from consuming excessive CPU.
/// </summary>
public static class DefenderService
{
    private static bool _alreadyHandled;

    /// <summary>
    /// Add SpoofDPI exclusions to Defender once. Requires UAC elevation.
    /// Uses a marker file so it only prompts once across app restarts.
    /// </summary>
    public static async Task EnsureExcludedAsync(Models.AppState appState)
    {
        if (_alreadyHandled) return;
        _alreadyHandled = true;

        if (File.Exists(GetMarkerPath())) return;

        await Task.Run(() =>
        {
            appState.AddLog("Adding Defender exclusion for SpoofDPI...", Models.LogLevel.Info);

            // Write a temp .ps1 script to avoid quoting issues
            var scriptPath = Path.Combine(Path.GetTempPath(), "punchthrough_defender.ps1");
            var markerPath = GetMarkerPath();
            var spoofDpiPath = SpoofDPIManager.BinaryPath;

            try
            {
                // Script adds exclusions and writes marker file on success
                File.WriteAllText(scriptPath,
                    $"try {{\n" +
                    $"    Add-MpPreference -ExclusionProcess 'spoofdpi.exe' -ErrorAction Stop\n" +
                    $"    Add-MpPreference -ExclusionPath '{spoofDpiPath}' -ErrorAction Stop\n" +
                    $"    [System.IO.File]::WriteAllText('{markerPath}', (Get-Date -Format o))\n" +
                    $"}} catch {{\n" +
                    $"    exit 1\n" +
                    $"}}\n");

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(15000);

                    // Check if marker was created by the script (= success)
                    if (File.Exists(markerPath))
                        appState.AddLog("Defender exclusion added successfully");
                    else
                        appState.AddLog("Defender exclusion may have failed", Models.LogLevel.Warning);
                }
            }
            catch
            {
                appState.AddLog("Defender exclusion skipped (UAC declined)", Models.LogLevel.Warning);
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
            }
        });
    }

    private static string GetMarkerPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PunchThrough", ".defender_exclusion_done");
}
