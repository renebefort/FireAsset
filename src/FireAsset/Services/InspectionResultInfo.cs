using FireAsset.Data.Entities;

namespace FireAsset.Services;

/// <summary>Anzeigenamen für Prüfergebnisse.</summary>
public static class InspectionResultInfo
{
    public record Option(InspectionResult Value, string Label);

    public static readonly Option[] All =
    {
        new(InspectionResult.Bestanden, "Bestanden"),
        new(InspectionResult.Mangelhaft, "Mangelhaft"),
        new(InspectionResult.NichtBestanden, "Nicht bestanden"),
    };

    public static string Label(InspectionResult? result) =>
        result is null ? "—" : All.FirstOrDefault(o => o.Value == result)?.Label ?? result.ToString()!;

    /// <summary>Radzen BadgeStyle passend zum Ergebnis.</summary>
    public static Radzen.BadgeStyle BadgeStyle(InspectionResult? result) => result switch
    {
        InspectionResult.Bestanden => Radzen.BadgeStyle.Success,
        InspectionResult.Mangelhaft => Radzen.BadgeStyle.Warning,
        InspectionResult.NichtBestanden => Radzen.BadgeStyle.Danger,
        _ => Radzen.BadgeStyle.Light,
    };
}
