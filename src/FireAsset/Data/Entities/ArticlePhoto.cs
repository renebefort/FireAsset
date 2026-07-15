namespace FireAsset.Data.Entities;

/// <summary>
/// Foto eines Artikels (ein Bild je Artikel). Liegt bewusst in einer eigenen Tabelle, damit
/// die Blobs die Artikel-Listenabfragen nicht belasten. Es werden zwei serverseitig verkleinerte
/// Varianten gespeichert: eine Detailgröße (max. 1024 px) und ein Grid-Thumbnail (128 px), beide JPEG.
/// </summary>
public class ArticlePhoto
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    /// <summary>MIME-Typ der gespeicherten Varianten (immer "image/jpeg").</summary>
    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>Detailvariante (längste Kante max. 1024 px).</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Grid-Thumbnail (längste Kante max. 128 px).</summary>
    public byte[] Thumbnail { get; set; } = Array.Empty<byte>();

    /// <summary>Größe der Detailvariante in Bytes.</summary>
    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}
