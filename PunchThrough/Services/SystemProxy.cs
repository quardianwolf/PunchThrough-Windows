using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace PunchThrough.Services;

/// <summary>
/// Sets the Windows "automatic proxy configuration script" (PAC) for the current user and
/// restores the previous value afterwards. The previous value is kept on disk so that a
/// crash never leaves browsers pointing at a dead local address.
/// </summary>
public static class SystemProxy
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string ValueName = "AutoConfigURL";

    private sealed class Backup
    {
        public string? AutoConfigUrl { get; set; }
    }

    private static string BackupPath => Path.Combine(AppPaths.DataDir, "proxy_backup.json");

    public static string? CurrentPacUrl
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
                return key?.GetValue(ValueName) as string;
            }
            catch { return null; }
        }
    }

    public static void SetPac(string url)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);

        if (!File.Exists(BackupPath)) // keep the true original across restarts of the tunnel
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(BackupPath,
                JsonSerializer.Serialize(new Backup { AutoConfigUrl = key.GetValue(ValueName) as string }));
        }

        key.SetValue(ValueName, url, RegistryValueKind.String);
        NotifyChanged();
    }

    public static void Restore()
    {
        if (!File.Exists(BackupPath)) return;

        try
        {
            string? original = null;
            try { original = JsonSerializer.Deserialize<Backup>(File.ReadAllText(BackupPath))?.AutoConfigUrl; }
            catch { }

            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            if (string.IsNullOrEmpty(original))
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            else
                key.SetValue(ValueName, original, RegistryValueKind.String);

            File.Delete(BackupPath);
            NotifyChanged();
        }
        catch { }
    }

    /// <summary>Repair proxy settings left behind by a crashed run. True when a repair happened.</summary>
    public static bool RecoverIfNeeded()
    {
        if (!File.Exists(BackupPath)) return false;
        Restore();
        return true;
    }

    private static void NotifyChanged()
    {
        const int InternetOptionSettingsChanged = 39;
        const int InternetOptionRefresh = 37;
        try
        {
            InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
        }
        catch { }
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int bufferLength);
}
