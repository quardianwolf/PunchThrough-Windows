using System.Diagnostics;
using System.IO;

namespace PunchThrough.Services;

/// <summary>
/// Manages launch-at-startup using Task Scheduler with elevated privileges.
/// Registry Run key doesn't work for admin-required apps (no UAC at boot).
/// </summary>
public static class StartupService
{
    private const string TaskName = "PunchThrough";
    private const string OldRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled, string? exePath = null)
    {
        // Clean up old Run registry entry from v1.x
        CleanupOldRegistry();

        if (enabled)
        {
            exePath ??= Environment.ProcessPath ?? "";
            CreateScheduledTask(exePath);
        }
        else
        {
            DeleteScheduledTask();
        }
    }

    private static void CreateScheduledTask(string exePath)
    {
        try
        {
            // Delete existing first to ensure clean state
            DeleteScheduledTask();

            // Create XML for task — easier than schtasks command-line for complex options
            var xml = BuildTaskXml(exePath);
            var xmlPath = Path.Combine(Path.GetTempPath(), "punchthrough_task.xml");
            File.WriteAllText(xmlPath, xml, System.Text.Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);

            try { File.Delete(xmlPath); } catch { }
        }
        catch { }
    }

    private static void DeleteScheduledTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Delete /TN \"{TaskName}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch { }
    }

    private static void CleanupOldRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(OldRegistryPath, writable: true);
            key?.DeleteValue(TaskName, throwOnMissingValue: false);
        }
        catch { }
    }

    private static string BuildTaskXml(string exePath)
    {
        var userId = Environment.UserDomainName + "\\" + Environment.UserName;
        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>PunchThrough</Author>
    <Description>PunchThrough DPI bypass — auto-start at login</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{System.Security.SecurityElement.Escape(userId)}</UserId>
      <Delay>PT5S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{System.Security.SecurityElement.Escape(userId)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{System.Security.SecurityElement.Escape(exePath)}</Command>
    </Exec>
  </Actions>
</Task>";
    }
}
