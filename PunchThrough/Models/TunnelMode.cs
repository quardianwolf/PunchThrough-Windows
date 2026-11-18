namespace PunchThrough.Models;

public enum TunnelMode
{
    Off,           // No tunnel
    SelectedSites, // Only the listed domains go through the server
    AllTraffic     // Everything that honours the system proxy goes through the server
}

public static class TunnelModeExtensions
{
    public static string GetDisplayName(this TunnelMode mode) => mode switch
    {
        TunnelMode.SelectedSites => Strings.Get("TunnelModeSites"),
        TunnelMode.AllTraffic => Strings.Get("TunnelModeAll"),
        _ => Strings.Get("TunnelModeOff")
    };

    public static string GetDescription(this TunnelMode mode) => mode switch
    {
        TunnelMode.SelectedSites => Strings.Get("TunnelModeSitesDesc"),
        TunnelMode.AllTraffic => Strings.Get("TunnelModeAllDesc"),
        _ => Strings.Get("TunnelModeOffDesc")
    };
}
