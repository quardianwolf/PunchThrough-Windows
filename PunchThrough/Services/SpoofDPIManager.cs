using System.IO;
using System.Reflection;

namespace PunchThrough.Services;

/// <summary>
/// Manages SpoofDPI binary. Extracts from embedded resource — no download needed.
/// </summary>
public static class SpoofDPIManager
{
    private const string PinnedVersion = "1.3.0";
    private const string BinaryName = "spoofdpi.exe";
    private const string EmbeddedResourceName = "PunchThrough.Assets.spoofdpi.exe";

    public static string BinDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PunchThrough", "bin");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string BinaryPath => Path.Combine(BinDirectory, BinaryName);
    private static string VersionFilePath => Path.Combine(BinDirectory, "spoofdpi.version");

    public static bool IsInstalled()
    {
        if (!File.Exists(BinaryPath)) return false;

        try
        {
            if (File.Exists(VersionFilePath))
            {
                var installed = File.ReadAllText(VersionFilePath).Trim();
                if (installed != PinnedVersion) return false;
            }
            else
            {
                return false;
            }
        }
        catch { }

        return true;
    }

    public static string GetPinnedVersion() => PinnedVersion;

    /// <summary>
    /// Extract SpoofDPI from embedded resource. No internet needed.
    /// </summary>
    public static async Task<(bool success, string message)> DownloadAsync(IProgress<int>? progress = null)
    {
        try
        {
            progress?.Report(10);

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);

            if (stream == null)
                return (false, "SpoofDPI binary not found in application resources.");

            progress?.Report(30);

            if (File.Exists(BinaryPath))
                File.Delete(BinaryPath);

            await using var fileStream = File.Create(BinaryPath);
            await stream.CopyToAsync(fileStream);

            progress?.Report(80);

            File.WriteAllText(VersionFilePath, PinnedVersion);

            progress?.Report(100);
            return (true, $"SpoofDPI v{PinnedVersion} extracted.");
        }
        catch (Exception ex)
        {
            return (false, $"Extraction failed: {ex.Message}");
        }
    }
}
