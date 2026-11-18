using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using PunchThrough.Models;

namespace PunchThrough.Services;

/// <summary>
/// Points every active network adapter at the selected DNS servers (optionally with
/// Windows' encrypted DoH), and puts things back exactly as they were afterwards.
///
/// The original configuration is written to disk BEFORE anything is changed, so a crash
/// or a killed process is repaired on the next start instead of leaving the user on our DNS.
/// </summary>
public static class DnsService
{
    public sealed record ApplyResult(int Interfaces, bool DohRequested, bool DohActive);

    public sealed class InterfaceBackup
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>Statically configured servers; empty means "automatic (DHCP)".</summary>
        public List<string> V4 { get; set; } = new();
        public List<string> V6 { get; set; } = new();
    }

    public sealed class Backup
    {
        public List<InterfaceBackup> Interfaces { get; set; } = new();
        /// <summary>DoH servers whose auto-upgrade we switched on (and must switch off again).</summary>
        public List<string> DohAddresses { get; set; } = new();
    }

    private static readonly object Gate = new();
    private static string BackupPath => Path.Combine(AppPaths.DataDir, "dns_backup.json");
    private const string HostsMarker = "# PunchThrough";

    private static DnsProfile? _activeProfile;

    /// <summary>
    /// Apply the profile to all active adapters. Throws when there is no adapter to configure.
    /// </summary>
    public static ApplyResult Apply(DnsProfile profile, bool enableDoH)
    {
        lock (Gate)
        {
            var nics = GetActiveInterfaces();
            if (nics.Count == 0)
                throw new InvalidOperationException(Strings.Get("ErrNoNetwork"));

            var backup = LoadBackup() ?? new Backup();
            foreach (var nic in nics)
                Remember(backup, nic);
            SaveBackup(backup);

            foreach (var nic in nics)
                SetServers(nic.Name, profile);

            var dohActive = false;
            if (enableDoH && profile.DohTemplate != null)
            {
                dohActive = EnableDoh(profile, backup);
                SaveBackup(backup);
            }

            _activeProfile = profile;
            FlushDns();
            Debug.WriteLine($"[DNS] {profile.Primary} applied to {nics.Count} adapter(s), DoH={dohActive}");
            return new ApplyResult(nics.Count, enableDoH, dohActive);
        }
    }

    /// <summary>
    /// Configure adapters that came up after <see cref="Apply"/> (Wi-Fi/Ethernet switch, docking...).
    /// Returns the number of newly configured adapters.
    /// </summary>
    public static int ApplyToNewInterfaces()
    {
        lock (Gate)
        {
            if (_activeProfile == null) return 0;

            var backup = LoadBackup();
            if (backup == null) return 0;

            var fresh = GetActiveInterfaces()
                .Where(n => backup.Interfaces.All(b => b.Id != n.Id))
                .ToList();
            if (fresh.Count == 0) return 0;

            foreach (var nic in fresh)
                Remember(backup, nic);
            SaveBackup(backup);

            foreach (var nic in fresh)
                SetServers(nic.Name, _activeProfile);

            FlushDns();
            return fresh.Count;
        }
    }

    /// <summary>Undo everything recorded in the backup file. Safe to call at any time.</summary>
    public static void Cleanup()
    {
        lock (Gate)
        {
            _activeProfile = null;
            CleanupHosts();

            var backup = LoadBackup();
            if (backup == null) return;

            foreach (var nic in backup.Interfaces)
            {
                RestoreFamily(nic.Name, "ipv4", nic.V4);
                RestoreFamily(nic.Name, "ipv6", nic.V6);
            }

            if (backup.DohAddresses.Count > 0)
            {
                var script = new StringBuilder();
                foreach (var address in backup.DohAddresses)
                    script.AppendLine($"Set-DnsClientDohServerAddress -ServerAddress '{address}' -AutoUpgrade $false -ErrorAction SilentlyContinue");
                ProcessUtil.RunPowerShell(script.ToString());
            }

            try { File.Delete(BackupPath); } catch { }
            FlushDns();
            Debug.WriteLine("[DNS] Original DNS restored");
        }
    }

    /// <summary>
    /// If a previous run died without restoring DNS, do it now. Returns true when a repair happened.
    /// </summary>
    public static bool RecoverIfNeeded()
    {
        if (!File.Exists(BackupPath)) return false;
        Cleanup();
        return true;
    }

    // --- Adapters ---

    private static List<NetworkInterface> GetActiveInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    && n.GetIPProperties().GatewayAddresses.Count > 0)
                .ToList();
        }
        catch
        {
            return new List<NetworkInterface>();
        }
    }

    private static void Remember(Backup backup, NetworkInterface nic)
    {
        if (backup.Interfaces.Any(b => b.Id == nic.Id)) return; // keep the true original
        backup.Interfaces.Add(new InterfaceBackup
        {
            Id = nic.Id,
            Name = nic.Name,
            V4 = ReadStaticServers(nic.Id, "Tcpip"),
            V6 = ReadStaticServers(nic.Id, "Tcpip6")
        });
    }

    /// <summary>
    /// The registry holds only manually configured servers (empty = DHCP), which is exactly
    /// what has to be restored — and unlike netsh output it does not depend on the UI language.
    /// </summary>
    private static List<string> ReadStaticServers(string interfaceId, string stack)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{stack}\Parameters\Interfaces\{interfaceId}");
            var value = key?.GetValue("NameServer") as string ?? "";
            return value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void SetServers(string name, DnsProfile profile)
    {
        // A family without addresses (custom single-address DNS) is left untouched
        if (profile.V4.Length > 0) SetStatic(name, "ipv4", profile.V4);
        if (profile.V6.Length > 0) SetStatic(name, "ipv6", profile.V6);
    }

    private static void RestoreFamily(string name, string family, List<string> original)
    {
        if (original.Count == 0)
            Netsh($"interface {family} set dnsservers name=\"{name}\" source=dhcp");
        else
            SetStatic(name, family, original);
    }

    private static void SetStatic(string name, string family, IReadOnlyList<string> servers)
    {
        Netsh($"interface {family} set dnsservers name=\"{name}\" source=static address={servers[0]} register=primary validate=no");
        for (var i = 1; i < servers.Count; i++)
            Netsh($"interface {family} add dnsservers name=\"{name}\" address={servers[i]} index={i + 1} validate=no");
    }

    private static void Netsh(string arguments) => ProcessUtil.Run("netsh", arguments, 10000);

    private static void FlushDns() => ProcessUtil.Run("ipconfig", "/flushdns", 5000);

    // --- DoH ---

    /// <summary>
    /// Turn on DoH auto-upgrade for the profile's servers. Returns false on Windows versions
    /// without system DoH (Windows 10) — DNS then still goes to the chosen server, unencrypted.
    /// </summary>
    private static bool EnableDoh(DnsProfile profile, Backup backup)
    {
        var addresses = string.Join(",", profile.AllAddresses.Select(a => $"'{a}'"));
        var script =
            "if (-not (Get-Command Set-DnsClientDohServerAddress -ErrorAction SilentlyContinue)) { 'UNSUPPORTED'; exit }\n" +
            $"$template = '{profile.DohTemplate}'\n" +
            $"foreach ($a in @({addresses})) {{\n" +
            "  $existing = Get-DnsClientDohServerAddress -ServerAddress $a -ErrorAction SilentlyContinue\n" +
            "  if ($existing -and $existing.AutoUpgrade) { continue }\n" + // user already had it on — not ours to undo
            "  try {\n" +
            "    if ($existing) { Set-DnsClientDohServerAddress -ServerAddress $a -DohTemplate $template -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction Stop }\n" +
            "    else { Add-DnsClientDohServerAddress -ServerAddress $a -DohTemplate $template -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction Stop }\n" +
            "    \"CHANGED $a\"\n" +
            "  } catch { }\n" +
            "}\n" +
            "'DONE'\n";

        var (_, output) = ProcessUtil.RunPowerShell(script);
        if (!output.Contains("DONE")) return false;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("CHANGED ")) continue;
            var address = trimmed["CHANGED ".Length..];
            if (!backup.DohAddresses.Contains(address))
                backup.DohAddresses.Add(address);
        }
        return true;
    }

    // --- Backup file ---

    private static Backup? LoadBackup()
    {
        try
        {
            return File.Exists(BackupPath)
                ? JsonSerializer.Deserialize<Backup>(File.ReadAllText(BackupPath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveBackup(Backup backup)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(BackupPath, JsonSerializer.Serialize(backup));
    }

    // --- Hosts file ---

    /// <summary>Remove hosts entries written by PunchThrough 1.x (no longer created).</summary>
    private static void CleanupHosts()
    {
        try
        {
            var hostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            if (!File.Exists(hostsPath)) return;

            var lines = new List<string>(File.ReadAllLines(hostsPath));
            if (lines.RemoveAll(l => l.Contains(HostsMarker)) > 0)
                File.WriteAllLines(hostsPath, lines);
        }
        catch { }
    }
}
