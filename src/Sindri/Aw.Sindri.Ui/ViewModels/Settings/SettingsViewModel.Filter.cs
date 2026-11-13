namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>The Settings filter box (#3362): what is typed and which sections it keeps.</summary>
internal sealed partial class SettingsViewModel
{
    /// <summary>What the filter box holds; empty shows every section (#3362).</summary>
    public string Filter
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            OnChanged(nameof(Filter));
            NotifyShown();
        }
    } = string.Empty;

    /// <summary>True when the page has the setup checklist and the filter keeps it.</summary>
    public bool ShowsSetup => Shows(SettingsSection.Setup);

    /// <summary>True when the page has the model-choice line and the filter keeps the choices.</summary>
    public bool ShowsModelChoice => HasModelChoice && SettingsFilter.Matches(SettingsSection.Choices, Filter, _shown[(int)SettingsSection.Choices]);

    /// <summary>True when the page has the label-choice line and the filter keeps the choices.</summary>
    public bool ShowsLabelChoice => HasLabelChoice && SettingsFilter.Matches(SettingsSection.Choices, Filter, _shown[(int)SettingsSection.Choices]);

    /// <summary>True when the page has the product-surface-choice line and the filter keeps the choices.</summary>
    public bool ShowsSurfaceChoice => HasSurfaceChoice && SettingsFilter.Matches(SettingsSection.Choices, Filter, _shown[(int)SettingsSection.Choices]);

    /// <summary>True when the page has the Sandbox section and the filter keeps it.</summary>
    public bool ShowsSandbox => Shows(SettingsSection.Sandbox);

    /// <summary>True when the page has the Library card and the filter keeps it.</summary>
    public bool ShowsLibrary => Shows(SettingsSection.Library);

    /// <summary>True when the page has a Connections card and the filter keeps the group.</summary>
    public bool ShowsConnections => Shows(SettingsSection.Connections);

    /// <summary>The filter box's placeholder.</summary>
    public string FilterPlaceholder { get; } = Labels.SettingsFilterPlaceholder;

    /// <summary>The line under the no-match line: how to clear the filter.</summary>
    public string NoMatchHint { get; } = Labels.SettingsNoMatchHint;

    /// <summary>The line shown when no section matches the filter.</summary>
    public string NoMatchText => Labels.SettingsNoMatch(Filter.Trim());

    /// <summary>True when the "This app" section (Reduce motion, Text size) stays under the filter.</summary>
    public bool ShowsThisApp => Shows(SettingsSection.ThisApp);

    /// <summary>True when a filter is typed and no section on this page matches it.</summary>
    public bool HasNoMatch => Filter.Trim().Length > 0 && !Enum.GetValues<SettingsSection>().Any(Shows);

    // A section is shown when this page has it and the filter keeps it.
    private bool Shows(SettingsSection section) => section switch
    {
        SettingsSection.Setup => HasSetup,
        SettingsSection.Choices => HasModelChoice || HasLabelChoice || HasSurfaceChoice,
        SettingsSection.Sandbox => HasSandbox,
        SettingsSection.Library => HasLibrary,
        SettingsSection.Connections => HasConnections,
        SettingsSection.ThisApp => true,
        _ => false,
    } && SettingsFilter.Matches(section, Filter, _shown[(int)section]);

    // What each section shows now, read from the page by the view so the filter matches the words a person sees (#3362).
    private readonly string[] _shown = [.. Enumerable.Repeat(string.Empty, Enum.GetValues<SettingsSection>().Length)];

    /// <summary>Records the text a section shows; the filter matches it besides the section's synonyms.</summary>
    /// <param name="section">The section.</param>
    /// <param name="text">Its headings, row labels, buttons and descriptions.</param>
    internal void SetShownText(SettingsSection section, string text) => _shown[(int)section] = text;

    private void NotifyShown()
    {
        OnChanged(nameof(ShowsSetup));
        OnChanged(nameof(ShowsModelChoice));
        OnChanged(nameof(ShowsLabelChoice));
        OnChanged(nameof(ShowsSurfaceChoice));
        OnChanged(nameof(ShowsSandbox));
        OnChanged(nameof(ShowsLibrary));
        OnChanged(nameof(ShowsConnections));
        OnChanged(nameof(ShowsThisApp));
        OnChanged(nameof(HasNoMatch));
        OnChanged(nameof(NoMatchText));
    }
}
