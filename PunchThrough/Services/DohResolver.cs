using System.Net.Http;
using System.Text.Json;

namespace PunchThrough.Services;

/// <summary>Resolves A records over Cloudflare DoH, independent of the (possibly poisoned) system DNS.</summary>
public static class DohResolver
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<List<string>> ResolveAllAsync(string host)
    {
        var ips = new List<string>();
        try
        {
            var url = $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type=A";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/dns-json");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return ips;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("Answer", out var answers)) return ips;

            foreach (var answer in answers.EnumerateArray())
            {
                if (answer.GetProperty("type").GetInt32() != 1) continue;
                var ip = answer.GetProperty("data").GetString();
                if (!string.IsNullOrEmpty(ip)) ips.Add(ip);
            }
        }
        catch
        {
            // Unreachable / malformed answer — treated as "could not resolve"
        }
        return ips;
    }

    public static async Task<string?> ResolveAsync(string host) =>
        (await ResolveAllAsync(host)).FirstOrDefault();
}
