using System.Text;
using System.Text.Json;

namespace CRT.Core.Net;

/// <summary>
/// Speedrun.com REST v1 client. All requests: 10s timeout, paginated via
/// <c>max=200</c> + <c>pagination.links[rel=next]</c>, throttled to at most
/// 100 requests per minute. Callers run everything off the UI thread and map
/// failures to inline error states.
/// </summary>
public sealed class SpeedrunClient
{
    public const string BaseUrl = "https://www.speedrun.com/api/v1";
    public const string ApiKeySettingsUrl = "https://www.speedrun.com/settings/api-key";

    private const int MaxRequestsPerMinute = 100;

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _throttleLock = new(1, 1);
    private readonly Queue<DateTimeOffset> _requestTimes = new();

    public SpeedrunClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>The API key sent as <c>X-API-Key</c>; null when signed out.</summary>
    public string? ApiKey { get; set; }

    // ── Profile ────────────────────────────────────────────────────────────

    /// <summary>Validates an API key via GET /profile. Throws on failure.</summary>
    public async Task<SrcProfile> GetProfileAsync(string apiKey, CancellationToken ct = default)
    {
        using var document = await GetAsync($"{BaseUrl}/profile", apiKey, ct).ConfigureAwait(false);
        var data = document.RootElement.GetProperty("data");
        string id = data.GetProperty("id").GetString() ?? "";
        string name = data.GetProperty("names").GetProperty("international").GetString() ?? "";
        string? avatar = null;
        if (data.TryGetProperty("assets", out JsonElement assets) &&
            assets.TryGetProperty("image", out JsonElement image) &&
            image.ValueKind == JsonValueKind.Object &&
            image.TryGetProperty("uri", out JsonElement uri) &&
            uri.ValueKind == JsonValueKind.String)
        {
            avatar = uri.GetString();
        }
        return new SrcProfile(id, name, avatar);
    }

    // ── Moderated games / runs to verify ───────────────────────────────────

    public async Task<List<SrcGame>> GetModeratedGamesAsync(string userId, CancellationToken ct = default)
    {
        var games = new List<SrcGame>();
        await foreach (var data in PaginateAsync($"{BaseUrl}/games?moderator={Uri.EscapeDataString(userId)}&max=200", ct).ConfigureAwait(false))
        {
            foreach (var game in data.EnumerateArray())
            {
                games.Add(new SrcGame(
                    game.GetProperty("id").GetString() ?? "",
                    game.GetProperty("names").GetProperty("international").GetString() ?? ""));
            }
        }
        return games;
    }

    public async Task<List<SrcPendingRun>> GetPendingRunsForGameAsync(SrcGame game, CancellationToken ct = default)
    {
        var runs = new List<SrcPendingRun>();
        string url = $"{BaseUrl}/runs?status=new&game={Uri.EscapeDataString(game.Id)}" +
                     "&max=200&embed=players,category,level&orderby=submitted&direction=asc";
        await foreach (var data in PaginateAsync(url, ct).ConfigureAwait(false))
        {
            foreach (var run in data.EnumerateArray())
            {
                runs.Add(ParsePendingRun(run, game));
            }
        }
        return runs;
    }

    /// <summary>
    /// Fetches pending runs for every moderated game, at most 4 games in flight
    /// at a time, flattened into one list ordered by submission date.
    /// </summary>
    public async Task<List<SrcPendingRun>> GetAllPendingRunsAsync(IReadOnlyList<SrcGame> games, CancellationToken ct = default)
    {
        var gate = new SemaphoreSlim(4, 4);
        var tasks = games.Select(async game =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await GetPendingRunsForGameAsync(game, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results
            .SelectMany(r => r)
            .OrderBy(r => r.Submitted ?? DateTimeOffset.MaxValue)
            .ToList();
    }

    // ── My recent runs ─────────────────────────────────────────────────────

    public async Task<List<SrcRecentRun>> GetRecentRunsAsync(string userId, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/runs?user={Uri.EscapeDataString(userId)}" +
                     "&orderby=date&direction=desc&max=10&embed=game,category";
        using var document = await GetAsync(url, ApiKey, ct).ConfigureAwait(false);

        var runs = new List<SrcRecentRun>();
        foreach (var run in document.RootElement.GetProperty("data").EnumerateArray())
        {
            string gameName = "";
            if (run.TryGetProperty("game", out JsonElement game) &&
                game.TryGetProperty("data", out JsonElement gameData) &&
                gameData.ValueKind == JsonValueKind.Object &&
                gameData.TryGetProperty("names", out JsonElement names))
            {
                gameName = names.GetProperty("international").GetString() ?? "";
            }

            runs.Add(new SrcRecentRun(
                run.GetProperty("id").GetString() ?? "",
                gameName,
                ReadEmbeddedName(run, "category"),
                ReadPrimarySeconds(run),
                run.TryGetProperty("status", out JsonElement status) &&
                    status.TryGetProperty("status", out JsonElement statusValue)
                    ? statusValue.GetString() ?? ""
                    : "",
                run.TryGetProperty("date", out JsonElement date) && date.ValueKind == JsonValueKind.String
                    ? date.GetString()
                    : null,
                run.TryGetProperty("weblink", out JsonElement weblink) && weblink.ValueKind == JsonValueKind.String
                    ? weblink.GetString()
                    : null));
        }
        return runs;
    }

    // ── Verify / reject ────────────────────────────────────────────────────

    /// <summary>PUT /runs/{id}/status. Throws on failure.</summary>
    public async Task SetRunStatusAsync(string runId, bool verified, string? reason = null, CancellationToken ct = default)
    {
        object payload = verified
            ? new { status = new { status = "verified" } }
            : new { status = new { status = "rejected", reason = reason ?? "" } };

        await ThrottleAsync(ct).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/runs/{Uri.EscapeDataString(runId)}/status")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        AddHeaders(request, ApiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Speedrun.com returned {(int)response.StatusCode} while updating the run: {Truncate(body, 300)}");
        }
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private static SrcPendingRun ParsePendingRun(JsonElement run, SrcGame game)
    {
        var players = new List<string>();
        if (run.TryGetProperty("players", out JsonElement playersElement) &&
            playersElement.TryGetProperty("data", out JsonElement playersData) &&
            playersData.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in playersData.EnumerateArray())
            {
                if (player.TryGetProperty("names", out JsonElement names) &&
                    names.TryGetProperty("international", out JsonElement international))
                {
                    players.Add(international.GetString() ?? "");
                }
                else if (player.TryGetProperty("name", out JsonElement guestName))
                {
                    players.Add(guestName.GetString() ?? "");
                }
            }
        }

        string? videoUrl = null;
        if (run.TryGetProperty("videos", out JsonElement videos) &&
            videos.ValueKind == JsonValueKind.Object &&
            videos.TryGetProperty("links", out JsonElement links) &&
            links.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in links.EnumerateArray())
            {
                if (link.TryGetProperty("uri", out JsonElement uri) && uri.ValueKind == JsonValueKind.String)
                {
                    videoUrl = uri.GetString();
                    break;
                }
            }
        }

        DateTimeOffset? submitted = null;
        if (run.TryGetProperty("submitted", out JsonElement submittedElement) &&
            submittedElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(submittedElement.GetString(), out DateTimeOffset parsed))
        {
            submitted = parsed;
        }

        string? level = ReadEmbeddedName(run, "level");

        return new SrcPendingRun(
            run.GetProperty("id").GetString() ?? "",
            game.Id,
            game.Name,
            ReadEmbeddedName(run, "category"),
            string.IsNullOrEmpty(level) ? null : level,
            string.Join(", ", players.Where(p => p.Length > 0)),
            submitted,
            ReadPrimarySeconds(run),
            videoUrl,
            run.TryGetProperty("weblink", out JsonElement weblink) && weblink.ValueKind == JsonValueKind.String
                ? weblink.GetString()
                : null);
    }

    private static string ReadEmbeddedName(JsonElement run, string property)
    {
        if (run.TryGetProperty(property, out JsonElement embedded) &&
            embedded.ValueKind == JsonValueKind.Object &&
            embedded.TryGetProperty("data", out JsonElement data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("name", out JsonElement name) &&
            name.ValueKind == JsonValueKind.String)
        {
            return name.GetString() ?? "";
        }
        return "";
    }

    private static decimal ReadPrimarySeconds(JsonElement run)
    {
        if (run.TryGetProperty("times", out JsonElement times) &&
            times.TryGetProperty("primary_t", out JsonElement primary) &&
            primary.ValueKind == JsonValueKind.Number)
        {
            return primary.GetDecimal();
        }
        return 0m;
    }

    /// <summary>Follows pagination.links[rel=next], yielding each page's "data" array.</summary>
    private async IAsyncEnumerable<JsonElement> PaginateAsync(
        string firstUrl, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string? url = firstUrl;
        while (url is not null)
        {
            using var document = await GetAsync(url, ApiKey, ct).ConfigureAwait(false);
            // Clone so the element outlives the document disposal at loop end.
            yield return document.RootElement.GetProperty("data").Clone();

            url = null;
            if (document.RootElement.TryGetProperty("pagination", out JsonElement pagination) &&
                pagination.TryGetProperty("links", out JsonElement links) &&
                links.ValueKind == JsonValueKind.Array)
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("rel", out JsonElement rel) && rel.GetString() == "next" &&
                        link.TryGetProperty("uri", out JsonElement uri))
                    {
                        url = uri.GetString();
                        break;
                    }
                }
            }
        }
    }

    private async Task<JsonDocument> GetAsync(string url, string? apiKey, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, apiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    private static void AddHeaders(HttpRequestMessage request, string? apiKey)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", AppVersion.UserAgent);
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }
    }

    /// <summary>Sliding-window throttle: at most 100 requests in any 60 seconds.</summary>
    private async Task ThrottleAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan waitTime = TimeSpan.Zero;
            await _throttleLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow;
                while (_requestTimes.Count > 0 && (now - _requestTimes.Peek()) > TimeSpan.FromMinutes(1))
                {
                    _requestTimes.Dequeue();
                }
                if (_requestTimes.Count < MaxRequestsPerMinute)
                {
                    _requestTimes.Enqueue(now);
                    return;
                }
                waitTime = _requestTimes.Peek() + TimeSpan.FromMinutes(1) - now;
            }
            finally
            {
                _throttleLock.Release();
            }
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, ct).ConfigureAwait(false);
            }
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
