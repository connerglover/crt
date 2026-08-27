namespace CRT.Core.Net;

/// <summary>
/// YouTube Innertube client identities. YouTube rotates/retires these
/// periodically — keep them all in this one file so updating is a one-liner.
/// Verified working 2026-08: ANDROID primary, IOS fallback. Do NOT use the WEB
/// client (it returns zero formats).
/// </summary>
public static class InnertubeClients
{
    public const string PlayerEndpoint = "https://www.youtube.com/youtubei/v1/player";

    // Primary: ANDROID client.
    public const string AndroidClientName = "ANDROID";
    public const string AndroidClientVersion = "20.10.38";
    public const int AndroidSdkVersion = 30;
    public const string AndroidUserAgent =
        "com.google.android.youtube/" + AndroidClientVersion + " (Linux; U; Android 11) gzip";

    // Fallback: IOS client.
    public const string IosClientName = "IOS";
    public const string IosClientVersion = "20.10.4";
    public const string IosDeviceModel = "iPhone16,2";
    public const string IosUserAgent =
        "com.google.ios.youtube/" + IosClientVersion + " (" + IosDeviceModel + "; U; CPU iOS 17_5_1 like Mac OS X)";
}
