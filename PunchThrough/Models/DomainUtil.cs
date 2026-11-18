using System.Text.RegularExpressions;

namespace PunchThrough.Models;

public static class DomainUtil
{
    private static readonly Regex Valid = new(@"^[a-z0-9][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)+$", RegexOptions.Compiled);

    /// <summary>
    /// Turn whatever the user pasted ("https://www.example.com/page", "*.example.com")
    /// into a bare domain. Returns null when it is not a usable domain.
    /// </summary>
    public static string? Normalize(string? input)
    {
        var d = (input ?? "").Trim().ToLowerInvariant();
        var scheme = d.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) d = d[(scheme + 3)..];
        var end = d.IndexOfAny(new[] { '/', '?', '#', ':' });
        if (end >= 0) d = d[..end];
        if (d.StartsWith("*.")) d = d[2..];
        if (d.StartsWith("www.")) d = d[4..];
        d = d.Trim('.');
        return Valid.IsMatch(d) ? d : null;
    }
}
