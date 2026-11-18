using System.Net;
using System.Net.Sockets;

namespace PunchThrough.Models;

public enum DnsServer
{
    Google,
    Cloudflare,
    Quad9,
    Custom
}

/// <summary>Addresses (and optional DoH template) that get written to the network adapters.</summary>
public sealed record DnsProfile(string[] V4, string[] V6, string? DohTemplate)
{
    public IEnumerable<string> AllAddresses => V4.Concat(V6);
    public string Primary => V4.Length > 0 ? V4[0] : V6[0];
}

public static class DnsServerExtensions
{
    private static readonly DnsProfile Cloudflare = new(
        new[] { "1.1.1.1", "1.0.0.1" },
        new[] { "2606:4700:4700::1111", "2606:4700:4700::1001" },
        "https://cloudflare-dns.com/dns-query");

    private static readonly DnsProfile Google = new(
        new[] { "8.8.8.8", "8.8.4.4" },
        new[] { "2001:4860:4860::8888", "2001:4860:4860::8844" },
        "https://dns.google/dns-query");

    private static readonly DnsProfile Quad9 = new(
        new[] { "9.9.9.9", "149.112.112.112" },
        new[] { "2620:fe::fe", "2620:fe::9" },
        "https://dns.quad9.net/dns-query");

    /// <summary>
    /// Resolve the selected server to concrete addresses. A custom server is a single
    /// plain-DNS address (no DoH template is known for it); an invalid one falls back to Cloudflare.
    /// </summary>
    public static DnsProfile GetProfile(this DnsServer server, string customDns) => server switch
    {
        DnsServer.Google => Google,
        DnsServer.Quad9 => Quad9,
        DnsServer.Custom => ParseCustom(customDns) ?? Cloudflare,
        _ => Cloudflare
    };

    public static bool IsValidCustom(string customDns) => ParseCustom(customDns) != null;

    private static DnsProfile? ParseCustom(string customDns)
    {
        if (!IPAddress.TryParse(customDns?.Trim(), out var ip)) return null;
        return ip.AddressFamily == AddressFamily.InterNetworkV6
            ? new DnsProfile(Array.Empty<string>(), new[] { ip.ToString() }, null)
            : new DnsProfile(new[] { ip.ToString() }, Array.Empty<string>(), null);
    }

    public static string GetDisplayName(this DnsServer server) => server switch
    {
        DnsServer.Google => "Google (8.8.8.8)",
        DnsServer.Cloudflare => "Cloudflare (1.1.1.1)",
        DnsServer.Quad9 => "Quad9 (9.9.9.9)",
        DnsServer.Custom => Strings.Get("Custom"),
        _ => "Cloudflare (1.1.1.1)"
    };
}
