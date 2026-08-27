using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace CRT.Core.Settings;

/// <summary>
/// DPAPI-protected storage for the Speedrun.com API key
/// (<c>src_api_key.bin</c> next to settings.ini, CurrentUser scope).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CRT Speedrun.com");

    private readonly string _path;

    public ApiKeyStore(string path)
    {
        _path = path;
    }

    public bool HasKey => File.Exists(_path);

    public void Save(string apiKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        byte[] protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    /// <summary>Returns the stored key, or null when missing or undecryptable.</summary>
    public string? TryLoad()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            byte[] plain = ProtectedData.Unprotect(
                File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort.
        }
    }
}
