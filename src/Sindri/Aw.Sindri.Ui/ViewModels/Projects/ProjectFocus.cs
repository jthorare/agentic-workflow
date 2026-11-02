namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>Where the page puts focus after a write closed the sheet.</summary>
internal enum ProjectFocus
{
    /// <summary>A card, by id.</summary>
    Card,

    /// <summary>The Add project button.</summary>
    Add,

    /// <summary>The Hidden fold's button.</summary>
    HiddenFold,

    /// <summary>The Finished fold's button.</summary>
    FinishedFold,
}
