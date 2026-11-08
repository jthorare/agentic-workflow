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
        (Text, Due, Links) = (text, Iso(due), string.Join(", ", refs));
    }

    /// <summary>Saves what changed (Enter in a field); the panel sets it.</summary>
    internal Func<Task> Save { get; init; } = static () => Task.CompletedTask;

    /// <summary>Closes the edit and sends nothing (Esc in a field); the panel sets it.</summary>
    internal Action Cancel { get; init; } = static () => { };

    /// <summary>The todo's id.</summary>
    public string Id { get; }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"Saving…".</summary>
    public string SavingText => Labels.TodoEditSaving;

    /// <summary>The text field's name for a screen reader.</summary>
    public string TextName => Labels.TodoEditTextName;

    /// <summary>The due field's name for a screen reader.</summary>
    public string DueName => Labels.TodoEditDueName;

    /// <summary>The due field's hint.</summary>
    public string DuePlaceholder => Labels.TodoEditDuePlaceholder;

    /// <summary>The links field's name for a screen reader.</summary>
    public string LinksName => Labels.TodoEditLinksName;

    /// <summary>The links field's hint.</summary>
    public string LinksPlaceholder => Labels.TodoEditLinksPlaceholder;
#pragma warning restore CA1822, MA0041

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

    /// <summary>After "changed somewhere else": the todo as it is now ("Now: text · day · links"), next to the fields; empty otherwise.</summary>
    public string Latest { get; internal set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the save has run longer than 300 ms: the row says "Saving…".</summary>
    public bool IsSaving { get; internal set => Set(ref field, value); }

    /// <summary>The text when the edit opened.</summary>
    internal string OriginalText { get; private set; }

    /// <summary>The due day when the edit opened.</summary>
    internal DateOnly? OriginalDue { get; private set; }

    /// <summary>The link refs when the edit opened.</summary>
    internal IReadOnlyList<string> OriginalRefs { get; private set; }

    /// <summary>After "changed somewhere else": a field the owner didn't touch takes the latest value, a typed one stays, and the next save diffs against the latest.</summary>
    /// <param name="latest">The todo as Sindri has it now.</param>
    /// <param name="refs">Its link refs.</param>
    /// <param name="now">The "Now: …" line.</param>
    internal void Rebase(Api.TodoView latest, IReadOnlyList<string> refs, string now)
    {
        if (string.Equals(Text, OriginalText, StringComparison.Ordinal))
        {
            Text = latest.Text;
        }

        if (string.Equals(Due, Iso(OriginalDue), StringComparison.Ordinal))
        {
            Due = Iso(latest.Due);
        }

        if (string.Equals(Links, string.Join(", ", OriginalRefs), StringComparison.Ordinal))
        {
            Links = string.Join(", ", refs);
        }

        (Version, OriginalText, OriginalDue, OriginalRefs, Latest) = (latest.Version, latest.Text, latest.Due, refs, now);
    }

    private static string Iso(DateOnly? day) => day?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
