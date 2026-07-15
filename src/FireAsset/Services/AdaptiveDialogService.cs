using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace FireAsset.Services;

/// <summary>
/// Dünner Wrapper um Radzens <see cref="DialogService"/>. Auf kleinen bzw.
/// Touch-Geräten (Viewport &lt;= 768px – derselbe Breakpoint, ab dem app.css den
/// Dialog zum Vollbild-Sheet macht) werden beim Öffnen <c>Draggable</c> und
/// <c>Resizable</c> abgeschaltet: Ziehen und Größenändern sind per Finger
/// unbrauchbar und auf einem Vollbild-Sheet ohnehin wirkungslos. Auf dem Desktop
/// bleibt das Verhalten unverändert.
///
/// Öffner-Komponenten injizieren diesen Service unter dem Namen <c>Dialogs</c>
/// statt des <see cref="DialogService"/> – die vorhandenen Aufrufe (OpenAsync,
/// Confirm) bleiben dadurch unverändert.
/// </summary>
public sealed class AdaptiveDialogService(DialogService dialogs, IJSRuntime js)
{
    public async Task<dynamic?> OpenAsync<T>(
        string title,
        Dictionary<string, object?>? parameters = null,
        DialogOptions? options = null) where T : ComponentBase
    {
        if (options is not null && await IsCompactAsync())
        {
            options.Draggable = false;
            options.Resizable = false;
        }

        return await dialogs.OpenAsync<T>(title, parameters, options);
    }

    /// <summary>Reicht die Bestätigungsabfrage unverändert an Radzen weiter.</summary>
    public Task<bool?> Confirm(
        string message = "Confirm?",
        string title = "Confirm",
        ConfirmOptions? options = null)
        => dialogs.Confirm(message, title, options);

    /// <summary>
    /// True, wenn der Viewport &lt;= 768px ist. Fällt bei nicht verfügbarem
    /// JS-Interop (z. B. Prerender) bewusst auf Desktop-Verhalten zurück.
    /// </summary>
    private async Task<bool> IsCompactAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("faViewport.isCompact");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            return false;
        }
    }
}
