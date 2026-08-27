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
            if (!string.IsNullOrEmpty(latest) && latest != AppVersion.Version)
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
}
