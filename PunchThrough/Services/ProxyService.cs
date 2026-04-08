using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PunchThrough.Services;

public static class ProxyService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    // WinINet API to notify the system of proxy changes
    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    public static void SetProxy(bool enable, int port)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                key.SetValue("ProxyServer", $"127.0.0.1:{port}");
                // Bypass proxy for local addresses
                key.SetValue("ProxyOverride", "<local>;localhost;127.0.0.1");
            }
            else
            {
                key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                key.DeleteValue("ProxyServer", throwOnMissingValue: false);
                key.DeleteValue("ProxyOverride", throwOnMissingValue: false);
            }

            // Notify the system that proxy settings have changed
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        catch
        {
            // Registry access may fail in restricted environments
        }
    }

    public static bool IsProxyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue("ProxyEnable");
            return value is int i && i == 1;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetCurrentProxyServer()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue("ProxyServer") as string;
        }
        catch
        {
            return null;
        }
    }
}
