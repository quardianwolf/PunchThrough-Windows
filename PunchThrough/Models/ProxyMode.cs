namespace PunchThrough.Models;

public enum ProxyMode
{
    Full,        // All traffic through SpoofDPI
    DiscordOnly, // Only Discord domains
    Custom       // User-selected domains
}

public static class ProxyModeExtensions
{
    public static string GetDisplayName(this ProxyMode mode) => mode switch
    {
        ProxyMode.Full => Strings.Get("ProxyModeFull"),
        ProxyMode.DiscordOnly => Strings.Get("ProxyModeDiscord"),
        ProxyMode.Custom => Strings.Get("ProxyModeCustom"),
        _ => mode.ToString()
    };
}

public static class KnownDomains
{
    public static readonly (string Category, string[] Domains)[] Presets =
    {
        ("Discord", new[] { "discord.com", "discord.gg", "discordapp.com", "discordapp.net", "discord.media" }),
        ("Twitter / X", new[] { "twitter.com", "x.com", "t.co", "twimg.com" }),
        ("Instagram", new[] { "instagram.com", "cdninstagram.com" }),
        ("Reddit", new[] { "reddit.com", "redd.it", "redditmedia.com", "redditstatic.com" }),
        ("TikTok", new[] { "tiktok.com", "tiktokcdn.com" }),
        ("YouTube", new[] { "youtube.com", "googlevideo.com", "ytimg.com", "youtu.be" }),
        ("Wikipedia", new[] { "wikipedia.org", "wikimedia.org" }),
        ("Patreon", new[] { "patreon.com", "patreonusercontent.com" }),
        ("Adult Sites", new[] { "pornhub.com", "phncdn.com", "xvideos.com", "xnxx.com", "xhamster.com", "modelhub.com", "pornhubpremium.com" }),
    };

    public static string[] DiscordDomains => Presets[0].Domains;
}
