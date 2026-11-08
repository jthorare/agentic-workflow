namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// One row's edit in place (todo spec §3.3, §3.4): the three fields the owner types in and the line under them. It lives beside the row, not in it,
/// so a read that lands mid-edit rewrites the row and never the field.
/// </summary>
internal sealed class TodoEditViewModel : ObservableObject
{
    internal TodoEditViewModel(string id, long version, string text, DateOnly? due, IReadOnlyList<string> refs)
    {
        (Id, Version, OriginalText, OriginalDue, OriginalRefs) = (id, version, text, due, refs);
        (Text, Due, Links) = (text, due?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, string.Join(", ", refs));
    }

    /// <summary>The todo's id.</summary>
    public string Id { get; }

    /// <summary>The version the save sends: the row's when the edit opened, then the latest one after a "changed somewhere else".</summary>
    public long Version { get; internal set; }

    /// <summary>The text field.</summary>
    public string Text { get; set => Set(ref field, value); }

    /// <summary>The due field: a day word ("fri", "tomorrow", "11-10", "2026-11-10"); empty clears the date.</summary>
    public string Due { get; set => Set(ref field, value); }

    /// <summary>The links field: refs separated by commas.</summary>
    public string Links { get; set => Set(ref field, value); }

    /// <summary>The line under the fields: why a save did not happen; empty otherwise.</summary>
    public string Message { get; internal set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the save has run longer than 300 ms: the row says "Saving…".</summary>
    public bool IsSaving { get; internal set => Set(ref field, value); }

    /// <summary>The text when the edit opened.</summary>
    internal string OriginalText { get; }

    /// <summary>The due day when the edit opened.</summary>
    internal DateOnly? OriginalDue { get; }

    /// <summary>The link refs when the edit opened.</summary>
    internal IReadOnlyList<string> OriginalRefs { get; }
}
