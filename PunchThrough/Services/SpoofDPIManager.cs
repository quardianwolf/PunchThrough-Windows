using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace PunchThrough.Services;

/// <summary>
/// Manages SpoofDPI binary download. Uses a pinned stable version — no auto-update.
/// </summary>
public static class SpoofDPIManager
{
    private static readonly HttpClient Http = new();

    // v1.3.0 built from source for Windows (official repo doesn't ship Windows binaries for v1.x)
    private const string PinnedVersion = "1.3.0";
    private const string PinnedTag = "v1.3.0";
    private const string DownloadUrl =
        $"https://github.com/quardianwolf/SpoofDPI-Windows/releases/download/{PinnedTag}/spoofdpi-windows-amd64.exe";

    private const string BinaryName = "spoofdpi.exe";

    /// <summary>
    /// %LocalAppData%/PunchThrough/bin/
    /// </summary>
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

    public static bool IsInstalled() => File.Exists(BinaryPath);

    public static string GetPinnedVersion() => PinnedVersion;

    /// <summary>
    /// Download pinned SpoofDPI version from GitHub.
    /// </summary>
    public static async Task<(bool success, string message)> DownloadAsync(IProgress<int>? progress = null)
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.Clear();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("PunchThrough/1.0");

            progress?.Report(5);

            // Download exe directly (v0.12.2 ships a standalone .exe, not a zip)
            try
            {
                using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                    return (false, $"Download failed: HTTP {(int)response.StatusCode}");

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var stream = await response.Content.ReadAsStreamAsync();

                if (File.Exists(BinaryPath))
                    File.Delete(BinaryPath);

                await using var fileStream = File.Create(BinaryPath);

                var buffer = new byte[81920];
                long downloaded = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;

                    if (totalBytes > 0)
                    {
                        var pct = (int)(5 + (downloaded * 90.0 / totalBytes));
                        progress?.Report(Math.Min(pct, 95));
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Download failed: {ex.Message}");
            }

            progress?.Report(100);
            return (true, $"SpoofDPI v{PinnedVersion} installed.");
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected error: {ex.Message}");
        }
    }
}
