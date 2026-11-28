namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The archived state of the open workflow (<see cref="RunsViewModel"/>, continued).</summary>
internal sealed partial class RunsViewModel
{
    /// <summary>True when the open workflow is archived.</summary>
    public bool IsArchived => Shown?.ArchivedAt is not null;

    /// <summary>True when Run now is offered: not on an archived workflow.</summary>
    public bool ShowsRunNow => !IsArchived;
}
