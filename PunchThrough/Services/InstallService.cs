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
}
