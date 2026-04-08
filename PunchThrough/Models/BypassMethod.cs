namespace PunchThrough.Models;

public enum BypassMethod
{
    SpoofDPI
}

public enum DnsServer
{
    Google,
    Cloudflare,
    Quad9,
    Custom
}

public static class DnsServerExtensions
{
    public static string GetAddress(this DnsServer server) => server switch
    {
        DnsServer.Google => "8.8.8.8",
        DnsServer.Cloudflare => "1.1.1.1",
        DnsServer.Quad9 => "9.9.9.9",
        DnsServer.Custom => "",
        _ => "8.8.8.8"
    };

    public static string GetDisplayName(this DnsServer server) => server switch
    {
        DnsServer.Google => "Google (8.8.8.8)",
        DnsServer.Cloudflare => "Cloudflare (1.1.1.1)",
        DnsServer.Quad9 => "Quad9 (9.9.9.9)",
        DnsServer.Custom => Strings.Get("Custom"),
        _ => "Google (8.8.8.8)"
    };
}
