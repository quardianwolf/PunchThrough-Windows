using PunchThrough.Models;

namespace PunchThrough.Services;

public class BypassService
{
    private static readonly Lazy<BypassService> _instance = new(() => new BypassService());
    public static BypassService Instance => _instance.Value;

    private readonly ZapretService _zapret = new();

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
        appState.AddLog($"Starting DPI bypass ({appState.ProxyMode.GetDisplayName()})...");

        try
        {
            // Step 1: Set up DNS (system DoH for Full, hosts for selective)
            appState.AddLog("Setting up secure DNS...");
            await DnsService.SetupDns(appState.ProxyMode, appState.CustomProxyDomains.ToArray());
            appState.AddLog("DNS ready");

            // Step 2: Start zapret for TLS bypass
            await Task.Run(() =>
                _zapret.Start(appState.ProxyMode, appState.CustomProxyDomains.ToArray()));

            appState.ConnectionStatus = ConnectionStatus.Connected;
            appState.AddLog($"DPI bypass active — {appState.ProxyMode.GetDisplayName()}");
        }
        catch (Exception ex)
        {
            appState.ConnectionStatus = ConnectionStatus.CreateError(ex.Message);
            appState.AddLog($"Connection failed: {ex.Message}", LogLevel.Error);
        }
    }

    public async Task DisconnectAsync(AppState appState)
    {
        if (!appState.ConnectionStatus.CanToggle) return;

        appState.ConnectionStatus = ConnectionStatus.Disconnecting;
        appState.AddLog("Stopping DPI bypass...");

        await Task.Run(() =>
        {
            _zapret.Stop();
            DnsService.Cleanup();
        });

        appState.ConnectionStatus = ConnectionStatus.Disconnected;
        appState.AddLog("DPI bypass stopped");
    }

    public bool IsRunning() => _zapret.IsRunning;
}
