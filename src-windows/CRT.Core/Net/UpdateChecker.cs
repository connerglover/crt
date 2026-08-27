using System.Net.Http.Headers;
using System.Text.Json;

namespace CRT.Core.Net;

/// <summary>
/// Startup update check — ported from <c>src/crt/updater.py</c>. Silent on any
/// failure: this runs on every launch and a flaky connection must never
/// interrupt using the app.
/// </summary>
public sealed class UpdateChecker
{
    private readonly HttpClient _http;

    public UpdateChecker(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Returns the latest release tag when it differs from the running version,
    /// otherwise null (including on any network/parse failure).
    /// </summary>
    public async Task<string?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppVersion.RepoLatestReleaseApi);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("crt", AppVersion.Version));
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            string? latest = document.RootElement.GetProperty("tag_name").GetString();
            if (!string.IsNullOrEmpty(latest) && IsNewer(latest, AppVersion.Version))
            {
                return latest;
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Silent by design.
        }
        return null;
    }

    /// <summary>
    /// True when <paramref name="latestTag"/> is a strictly newer release than
    /// <paramref name="current"/>.
    /// </summary>
    /// <remarks>
    /// This used to be <c>latest != current</c>. Plain inequality was harmless
    /// while the app version tracked the published releases, but the native
    /// rewrite is 2.0.0 and the newest published tag is 1.2.2 — so every launch
    /// advertised an "update" that is actually a downgrade to the Python build.
    /// Unparseable tags return false: nagging on a tag we cannot understand is
    /// worse than staying quiet.
    /// </remarks>
    public static bool IsNewer(string latestTag, string current) =>
        TryParseVersion(latestTag, out Version? latest) &&
        TryParseVersion(current, out Version? running) &&
        latest > running;

    private static bool TryParseVersion(string text, out Version? version)
    {
        version = null;
        string trimmed = text.Trim().TrimStart('v', 'V');

        // Keep only the leading dotted-numeric run so pre-release suffixes
        // ("2.1.0-beta1", "1.2.2+build") still compare on their numeric part.
        int end = 0;
        while (end < trimmed.Length && (char.IsAsciiDigit(trimmed[end]) || trimmed[end] == '.'))
        {
            end++;
        }
        trimmed = trimmed[..end].TrimEnd('.');

        return trimmed.Contains('.') && Version.TryParse(trimmed, out version);
    }
}
