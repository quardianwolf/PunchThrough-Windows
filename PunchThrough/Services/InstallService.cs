using System.Diagnostics;
using System.IO;

namespace PunchThrough.Services;

public static class InstallService
{
    public static string InstallDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "PunchThrough");

    public static string InstalledExePath =>
        Path.Combine(InstallDirectory, "PunchThrough.exe");

    public static string DesktopShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PunchThrough.lnk");

    /// <summary>
    /// Returns true if the app is running from the installed location.
    /// </summary>
    public static bool IsInstalled()
    {
        var currentExe = Environment.ProcessPath;
        if (currentExe == null) return false;

        return string.Equals(
            Path.GetFullPath(currentExe),
            Path.GetFullPath(InstalledExePath),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the app has been installed previously (exe exists at install path).
    /// </summary>
    public static bool InstallExists() => File.Exists(InstalledExePath);

    /// <summary>
    /// Remove everything PunchThrough put on this machine: startup task, shortcut, settings,
    /// the zapret engine and the installed exe. The folders are deleted by a detached command
    /// a few seconds later, because the running exe and the WinDivert driver are still in use
    /// until this process has exited.
    /// </summary>
    public static void Uninstall()
    {
        StartupService.SetEnabled(false);

        try { File.Delete(DesktopShortcutPath); } catch { }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping -n 4 127.0.0.1 >nul" +
                            $" & rmdir /s /q \"{AppPaths.DataDir}\"" +
                            $" & rmdir /s /q \"{InstallDirectory}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath()
            });
        }
        catch { }
    }
}
