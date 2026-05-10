using System.IO;
using System.Windows.Media.Imaging;

namespace OldenEra.TemplateEditor.Services;

public static class WpfPreviewAdapter
{
    public static BitmapImage ToBitmapImage(byte[] png)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static string GetSidecarPath(string rmgJsonPath) =>
        rmgJsonPath.EndsWith(".rmg.json", System.StringComparison.OrdinalIgnoreCase)
            ? rmgJsonPath[..^".rmg.json".Length] + ".png"
            : System.IO.Path.ChangeExtension(rmgJsonPath, ".png");
}
