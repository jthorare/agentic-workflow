namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>What a write that takes the card off the page (or back onto it) did.</summary>
internal enum ProjectDone
{
    /// <summary>The attachment was removed.</summary>
    Removed,

    /// <summary>A led project was hidden.</summary>
    Hidden,

    /// <summary>A hidden project was brought back.</summary>
    Unhidden,
}
