using Microsoft.Win32;

namespace PunchThrough.Services;

public static class StartupService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PunchThrough";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string? exePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                exePath ??= Environment.ProcessPath ?? "";
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch { }

        // Clean up any leftover scheduled task from previous versions
        CleanupScheduledTask();
    }

    private static void CleanupScheduledTask()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Delete /TN \"{AppName}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(2000);
        }
        catch { }
    }
}
