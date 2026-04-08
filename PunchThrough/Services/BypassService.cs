using PunchThrough.Models;

namespace PunchThrough.Services;

public class BypassService
{
    private static readonly Lazy<BypassService> _instance = new(() => new BypassService());
    public static BypassService Instance => _instance.Value;

    private readonly SpoofDPIService _spoofDpi = new();

    private BypassService() { }

    public async Task ToggleAsync(AppState appState)
    {
        switch (appState.ConnectionStatus.State)
        {
            case ConnectionState.Connected:
                await DisconnectAsync(appState);
                break;
            case ConnectionState.Disconnected:
            case ConnectionState.Error:
                await ConnectAsync(appState);
                break;
        }
    }

    public async Task ConnectAsync(AppState appState)
    {
        if (!appState.ConnectionStatus.CanToggle) return;

        appState.ConnectionStatus = ConnectionStatus.Connecting;
        appState.AddLog("Starting SpoofDPI...");

        // Ensure Defender exclusion is in place to avoid CPU spikes
        await DefenderService.EnsureExcludedAsync(appState);

        // Try up to 2 times — first attempt sometimes fails during cold start
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await _spoofDpi.StartAsync(
                    appState.SpoofDpiPort,
                    appState.EffectiveDns,
                    appState.EnableDoH,
                    appState.EnableSystemProxy,
                    appState
                );

                appState.ConnectionStatus = ConnectionStatus.Connected;
                appState.AddLog("Successfully connected via SpoofDPI");
                return;
            }
            catch (Exception ex)
            {
                if (attempt == 1)
                {
                    appState.AddLog("First attempt failed, retrying...", LogLevel.Warning);
                    await Task.Delay(1000);
                }
                else
                {
                    appState.ConnectionStatus = ConnectionStatus.CreateError(ex.Message);
                    appState.AddLog($"Connection failed: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }

    public async Task DisconnectAsync(AppState appState)
    {
        if (!appState.ConnectionStatus.CanToggle) return;

        appState.ConnectionStatus = ConnectionStatus.Disconnecting;
        appState.AddLog("Stopping bypass...");

        await _spoofDpi.StopAsync(appState);

        appState.ConnectionStatus = ConnectionStatus.Disconnected;
        appState.AddLog("Disconnected successfully");
    }

    public bool IsSpoofDPIInstalled() => SpoofDPIService.IsSpoofDPIInstalled();
}
