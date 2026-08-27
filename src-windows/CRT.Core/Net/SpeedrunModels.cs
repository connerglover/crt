namespace CRT.Core.Net;

/// <summary>The authenticated user's Speedrun.com profile.</summary>
public sealed record SrcProfile(string Id, string Name, string? AvatarUri);

/// <summary>A game the user moderates.</summary>
public sealed record SrcGame(string Id, string Name);

/// <summary>A run awaiting verification.</summary>
public sealed record SrcPendingRun(
    string Id,
    string GameId,
    string GameName,
    string Category,
    string? Level,
    string Players,
    DateTimeOffset? Submitted,
    decimal PrimarySeconds,
    string? VideoUrl,
    string? WebLink);

/// <summary>One of the user's own recent runs.</summary>
public sealed record SrcRecentRun(
    string Id,
    string GameName,
    string Category,
    decimal PrimarySeconds,
    string Status,
    string? Date,
    string? WebLink);
