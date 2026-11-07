namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One key of a settings entry as the sheet shows it (panels, groups, collapse, risky, checks): where its value comes from, whether the entry sets it, a reset and a field error.</summary>
/// <param name="id">The key's name in the entry.</param>
/// <param name="title">The key's words.</param>
internal sealed class ReviewSettingsKey(string id, string title) : ObservableObject
{
    /// <summary>The key's name in the entry ("panels").</summary>
    public string Id { get; } = id;

    /// <summary>The key's words ("Panels").</summary>
    public string Title { get; } = title;

    /// <summary>"from this entry", "from default" or "from" and the entry's name.</summary>
    public string FromText
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(HeaderText));
        }
    } = string.Empty;

    /// <summary>"Panels: from default"; what a screen reader reads for the key.</summary>
    public string HeaderText => Labels.ReviewSettingsKeyLine(Title, FromText);

    /// <summary>True when the entry sets this key as it was read.</summary>
    public bool WasOwn { get; set; }

    /// <summary>The value's signature as it was read; the key is sent when it differs.</summary>
    public string Baseline { get; set; } = string.Empty;

    /// <summary>True after Reset to default: the key is left out of the entry.</summary>
    public bool IsReset
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowEditor));
        }
    }

    /// <summary>True while the value can be edited.</summary>
    public bool ShowEditor => !IsReset;

    /// <summary>The server's detail when it refused this key; empty otherwise.</summary>
    public string Error { get; set => Set(ref field, value); } = string.Empty;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The reset button's words.</summary>
    public string ResetText => Labels.ReviewSettingsReset;

    /// <summary>The note under a reset key.</summary>
    public string ResetNote => Labels.ReviewSettingsResetNote;
#pragma warning restore CA1822, MA0041
}
