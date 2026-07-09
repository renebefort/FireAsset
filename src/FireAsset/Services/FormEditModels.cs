using FireAsset.Data.Entities;

namespace FireAsset.Services;

/// <summary>Bearbeitbares Abbild eines Formularfelds (unabhängig von der persistierten Entität).</summary>
public class FormFieldModel
{
    public string Label { get; set; } = string.Empty;
    public FieldType FieldType { get; set; } = FieldType.YesNo;
    public string? ReferenceValue { get; set; }
    public string? Unit { get; set; }
    public bool ShowLastValue { get; set; }

    public FormFieldModel Clone() => new()
    {
        Label = Label,
        FieldType = FieldType,
        ReferenceValue = ReferenceValue,
        Unit = Unit,
        ShowLastValue = ShowLastValue,
    };

    public static FormFieldModel FromEntity(FormField f) => new()
    {
        Label = f.Label,
        FieldType = f.FieldType,
        ReferenceValue = f.ReferenceValue,
        Unit = f.Unit,
        ShowLastValue = f.ShowLastValue,
    };
}

/// <summary>Gesamter Bearbeitungsstand eines Formulars (Metadaten + Felder in Reihenfolge).</summary>
public class FormEditModel
{
    public int? FormId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<FormFieldModel> Fields { get; set; } = new();
}
