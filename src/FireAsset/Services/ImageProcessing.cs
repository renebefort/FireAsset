using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FireAsset.Services;

/// <summary>
/// Serverseitige Bildaufbereitung für Artikel-Fotos: erzeugt aus dem hochgeladenen Bild eine
/// verkleinerte Detailvariante (max. 1024 px) und ein Grid-Thumbnail (max. 128 px), beide als JPEG.
/// Hält damit die Datenbank klein und die Grid-Anzeige schnell.
/// </summary>
public static class ImageProcessing
{
    private const int MaxDetailEdge = 1024;
    private const int MaxThumbEdge = 128;

    /// <summary>
    /// Obergrenze für die dekodierte Pixelzahl (~48 Megapixel). Schützt vor „Decompression-Bomben“:
    /// kleine, gültige Dateien, die zu riesigen Bildmaßen dekodieren und den Arbeitsspeicher sprengen.
    /// </summary>
    private const long MaxPixels = 48L * 1_000_000;

    public record ProcessedImage(byte[] Detail, byte[] Thumbnail);

    /// <summary>
    /// Verkleinert das Eingabebild. Gibt <c>null</c> zurück, wenn die Daten kein lesbares Bild sind
    /// oder die Bildmaße die zulässige Obergrenze überschreiten.
    /// </summary>
    public static ProcessedImage? TryProcess(byte[] input)
    {
        try
        {
            // Nur den Header lesen und die Maße prüfen, BEVOR das Vollbild dekodiert wird.
            var info = Image.Identify(input);
            if ((long)info.Width * info.Height > MaxPixels)
            {
                return null;
            }

            using var image = Image.Load(input);
            var detail = Encode(image, MaxDetailEdge, quality: 82);
            var thumb = Encode(image, MaxThumbEdge, quality: 75);
            return new ProcessedImage(detail, thumb);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    /// <summary>Kopiert das Bild, skaliert es (ohne Hochskalieren) auf die maximale Kantenlänge und kodiert als JPEG.</summary>
    private static byte[] Encode(Image source, int maxEdge, int quality)
    {
        using var clone = source.Clone(ctx =>
        {
            if (Math.Max(source.Width, source.Height) > maxEdge)
            {
                // ResizeMode.Max: passt das Bild seitenverhältnistreu in die Box ein.
                ctx.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxEdge, maxEdge) });
            }
        });

        using var ms = new MemoryStream();
        clone.SaveAsJpeg(ms, new JpegEncoder { Quality = quality });
        return ms.ToArray();
    }
}
