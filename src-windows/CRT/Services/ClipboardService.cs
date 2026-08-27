using Windows.ApplicationModel.DataTransfer;

namespace CRT.Services;

/// <summary>Clipboard access helpers.</summary>
public static class ClipboardService
{
    public static void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    public static async Task<string> GetTextAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                return await content.GetTextAsync();
            }
        }
        catch (Exception)
        {
            // Clipboard access can transiently fail (locked by another process).
        }
        return "";
    }
}
