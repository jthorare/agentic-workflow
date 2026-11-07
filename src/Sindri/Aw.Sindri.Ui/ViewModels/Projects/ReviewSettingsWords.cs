namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The fixed words of the Review settings section, as properties because the view binds them.</summary>
internal sealed class ReviewSettingsWords
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The section's heading.</summary>
    public string Heading => Labels.ReviewSettingsHeading;

    /// <summary>The name field's label.</summary>
    public string NameField => Labels.ReviewSettingsNameField;

    /// <summary>The extends field's label.</summary>
    public string ExtendsField => Labels.ReviewSettingsExtendsField;

    /// <summary>The repos field's label.</summary>
    public string ReposField => Labels.ReviewSettingsReposField;

    /// <summary>The collapse field's label.</summary>
    public string CollapseField => Labels.ReviewSettingsCollapseField;

    /// <summary>Under the groups while none is set.</summary>
    public string GroupsByFolder => Labels.ReviewSettingsGroupsByFolder;

    /// <summary>The add-group button.</summary>
    public string AddGroup => Labels.ReviewSettingsAddGroup;

    /// <summary>The add-risky-path button.</summary>
    public string AddRisk => Labels.ReviewSettingsAddRisk;

    /// <summary>The add-check button.</summary>
    public string AddCheck => Labels.ReviewSettingsAddCheck;

    /// <summary>The Reload button.</summary>
    public string Reload => Labels.ReviewSettingsReload;

    /// <summary>The discard button.</summary>
    public string Discard => Labels.ReviewSettingsDiscard;

    /// <summary>The note on a read-only section.</summary>
    public string ReadOnly => Labels.ReviewSettingsReadOnly;
#pragma warning restore CA1822, MA0041
}
