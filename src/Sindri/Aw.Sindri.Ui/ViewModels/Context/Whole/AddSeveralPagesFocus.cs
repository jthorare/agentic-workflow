namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Where focus goes in the "Add several pages" sheet.</summary>
internal enum AddSeveralPagesFocus
{
    /// <summary>The paste box.</summary>
    Text,

    /// <summary>The first row that has a reason (<see cref="AddSeveralPagesViewModel.ProblemRow"/>).</summary>
    ProblemRow,

    /// <summary>The first row's tick, else the Save all button (Accept all suggested, once its rows are drawn).</summary>
    FirstRow,

    /// <summary>The Save all button.</summary>
    SaveAll,

    /// <summary>The Keep editing button of the discard question.</summary>
    KeepEditing,
}
