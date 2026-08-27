using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRT.Core.Net;

/// <summary>
/// Looks up the true encoded framerate of a YouTube video/itag pair via the
/// Innertube player API (no yt-dlp dependency), with an optional yt-dlp
/// fallback. Returns null on any failure — callers treat that as "couldn't be
/// verified" and never block on it. Results are cached per (videoId, itag) for
/// the app session.
/// </summary>
public sealed class InnertubeClient
{
    private readonly HttpClient _http;
    private readonly Func<string, string, CancellationToken, Task<decimal?>>? _ytDlpFallback;
    private readonly ConcurrentDictionary<(string VideoId, string FormatId), decimal?> _cache = new();

    /// <param name="ytDlpFallback">
    /// Optional fallback invoked as (videoId, formatId) → fps when the
    /// Innertube call fails and a yt-dlp binary is available.
    /// </param>
    public InnertubeClient(HttpClient? http = null, Func<string, string, CancellationToken, Task<decimal?>>? ytDlpFallback = null)
    {
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(8);
        _ytDlpFallback = ytDlpFallback;
    }

    public async Task<decimal?> GetFormatFramerateAsync(string videoId, string formatId, CancellationToken ct = default)
    {
        var key = (videoId, formatId);
        if (_cache.TryGetValue(key, out decimal? cached))
        {
            return cached;
        }

        decimal? result = await LookupAsync(videoId, formatId, ct).ConfigureAwait(false);
        _cache[key] = result;
        return result;
    }

    private async Task<decimal?> LookupAsync(string videoId, string formatId, CancellationToken ct)
    {
        decimal? fps =
            await TryPlayerRequestAsync(videoId, formatId, BuildAndroidBody(videoId), InnertubeClients.AndroidUserAgent, ct).ConfigureAwait(false)
            ?? await TryPlayerRequestAsync(videoId, formatId, BuildIosBody(videoId), InnertubeClients.IosUserAgent, ct).ConfigureAwait(false);
        if (fps is not null)
        {
            return fps;
        }

        if (_ytDlpFallback is not null)
        {
            try
            {
                return await _ytDlpFallback(videoId, formatId, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return null;
            }
        }
        return null;
    }

    private static string BuildAndroidBody(string videoId) => JsonSerializer.Serialize(new
    {
        videoId,
        context = new
        {
            client = new
            {
                clientName = InnertubeClients.AndroidClientName,
                clientVersion = InnertubeClients.AndroidClientVersion,
                androidSdkVersion = InnertubeClients.AndroidSdkVersion,
                hl = "en",
            },
        },
    });

    private static string BuildIosBody(string videoId) => JsonSerializer.Serialize(new
    {
        videoId,
        context = new
        {
            client = new
            {
                clientName = InnertubeClients.IosClientName,
                clientVersion = InnertubeClients.IosClientVersion,
                deviceModel = InnertubeClients.IosDeviceModel,
                hl = "en",
            },
        },
    });

    private async Task<decimal?> TryPlayerRequestAsync(string videoId, string formatId, string body, string userAgent, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, InnertubeClients.PlayerEndpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("streamingData", out JsonElement streamingData))
            {
                return null;
            }

            return FindItagFps(streamingData, "formats", formatId)
                ?? FindItagFps(streamingData, "adaptiveFormats", formatId);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static decimal? FindItagFps(JsonElement streamingData, string listName, string formatId)
    {
        if (!streamingData.TryGetProperty(listName, out JsonElement formats) || formats.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var format in formats.EnumerateArray())
        {
            if (!format.TryGetProperty("itag", out JsonElement itag))
            {
                continue;
            }
            // "fmt" from debug info is a string — compare after string conversion.
            string itagText = itag.ValueKind == JsonValueKind.Number ? itag.GetRawText() : itag.GetString() ?? "";
            if (itagText != formatId)
            {
                continue;
            }
            if (format.TryGetProperty("fps", out JsonElement fps) && fps.ValueKind == JsonValueKind.Number)
            {
                return decimal.Parse(fps.GetRawText(), CultureInfo.InvariantCulture);
            }
            return null;
        }
        return null;
    }
}
