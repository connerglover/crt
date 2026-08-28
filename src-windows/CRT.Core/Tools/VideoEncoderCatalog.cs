using System.Text.RegularExpressions;

namespace CRT.Core.Tools;

/// <summary>A video encoder plus the arguments that set its quality.</summary>
/// <param name="Name">Encoder name as ffmpeg knows it.</param>
/// <param name="QualityArguments">Quality flags, which differ per encoder.</param>
public sealed record VideoEncoder(string Name, IReadOnlyList<string> QualityArguments);

/// <summary>
/// Picks a working H.264 encoder from whatever the ffmpeg in use actually has.
/// </summary>
/// <remarks>
/// The export used to hard-code <c>libx264</c>. That encoder is GPL-licensed, so
/// it is absent from LGPL ffmpeg builds — and those are common, both from
/// package managers and as the variant a project picks to keep its licensing
/// simple. Against one, every export failed at the end with ffmpeg's "Unknown
/// encoder" rather than up front.
/// </remarks>
public static partial class VideoEncoderCatalog
{
    /// <summary>
    /// Candidates in preference order.
    /// </summary>
    /// <remarks>
    /// libx264 first: it is the best quality per byte and the only one here that
    /// supports CRF, so the output is quality-targeted rather than
    /// bitrate-targeted. The rest are ordered by how widely they work.
    ///
    /// Hardware encoders (h264_nvenc, h264_qsv, h264_amf) are deliberately
    /// excluded. ffmpeg lists them whether or not the machine has the matching
    /// GPU, so selecting one on the strength of the listing alone would trade a
    /// clear early failure for an obscure late one.
    /// </remarks>
    public static IReadOnlyList<VideoEncoder> Candidates { get; } = new[]
    {
        new VideoEncoder("libx264", new[] { "-preset", "veryfast", "-crf", "18" }),
        new VideoEncoder("libopenh264", new[] { "-b:v", "8M" }),
        new VideoEncoder("h264_mf", new[] { "-b:v", "8M" }),
        new VideoEncoder("mpeg4", new[] { "-q:v", "3" }),
    };

    /// <summary>The encoder assumed when detection cannot run.</summary>
    public static VideoEncoder Default => Candidates[0];

    [GeneratedRegex(@"^\s*[VAS.][F.][S.][X.][B.][D.]\s+(\S+)", RegexOptions.Multiline)]
    private static partial Regex EncoderLineRegex();

    /// <summary>Parses the encoder names out of <c>ffmpeg -encoders</c> output.</summary>
    public static IReadOnlySet<string> ParseAvailable(string encodersOutput)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in EncoderLineRegex().Matches(encodersOutput ?? ""))
        {
            names.Add(match.Groups[1].Value);
        }
        return names;
    }

    /// <summary>
    /// Chooses the best candidate present in <paramref name="available"/>, or
    /// null when the build has none of them.
    /// </summary>
    public static VideoEncoder? Choose(IReadOnlySet<string> available) =>
        Candidates.FirstOrDefault(candidate => available.Contains(candidate.Name));

    /// <summary>
    /// Asks an ffmpeg binary what it can encode and picks from that.
    /// </summary>
    /// <remarks>
    /// Failure to run ffmpeg at all returns the default rather than null: the
    /// export that follows will produce a far better error than anything that
    /// could be reported from here.
    /// </remarks>
    public static async Task<VideoEncoder?> DetectAsync(string ffmpegPath, CancellationToken ct = default)
    {
        ProcessResult result;
        try
        {
            result = await ProcessRunner
                .RunAsync(ffmpegPath, new[] { "-hide_banner", "-encoders" }, ct)
                .ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Default;
        }

        // ffmpeg writes the list to stdout, but older builds use stderr.
        var available = ParseAvailable(result.StandardOutput + "\n" + result.StandardError);
        return available.Count == 0 ? Default : Choose(available);
    }
}
