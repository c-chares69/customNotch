using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CustomNotch.App.Cells;

/// <summary>Décode l'image d'une lecture en ImageSource figée, une fois : la pilule redessine à chaque tick et
/// la même pochette revient tant que la piste ne change pas — on garde le dernier tableau décodé (par référence :
/// c'est le même objet tant que la source n'a rien relu). Une image illisible rend null, et la cellule retombe sur
/// son glyph.</summary>
public static class CoverImage
{
    private static byte[]? _lastBytes;
    private static ImageSource? _lastImage;

    public static ImageSource? Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        if (ReferenceEquals(bytes, _lastBytes)) return _lastImage;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 128;
            image.StreamSource = new MemoryStream(bytes, writable: false);
            image.EndInit();
            image.Freeze();
            _lastBytes = bytes; _lastImage = image;
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException)
        {
            _lastBytes = bytes; _lastImage = null;
            return null;
        }
    }
}
