using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): while the views are stalled the list level says so, with the Rebuild action, instead of an empty list or a create form (#2550).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True while the views are stalled (the shell's banner says so too).</summary>
    public bool ViewsStalled { get; private set => Set(ref field, value); }

    /// <summary>The Rebuild views control of the shell's banner, or null when it has none.</summary>
    public ControlViewModel? Rebuild { get; private set => Set(ref field, value); }

    /// <summary>True when the stalled line carries Rebuild.</summary>
    public bool HasRebuild => Rebuild is not null;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>"The lists are stalled, so this may be out of date."</summary>
    public string StalledText => Labels.WfStalled;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the empty sentence and its actions show: the list is empty, the views are not stalled and the New workflow form is not open.</summary>
    public bool ShowsEmpty => IsEmpty && !ViewsStalled && !IsCreating;

    /// <summary>True when the header carries New workflow: on the list, and not when the empty state offers its own.</summary>
    public bool ShowsHeaderNew => IsList && !ShowsEmpty;

    /// <summary>True when the New workflow form shows.</summary>
    public bool ShowsCreateForm => IsCreating && !ViewsStalled;

    /// <summary>True when New workflow can be pressed.</summary>
    public bool CanOpenCreate => !ViewsStalled;

    /// <summary>New workflow's tooltip while it is off; null otherwise.</summary>
    public string? NewWorkflowTip => ViewsStalled ? Labels.WfStalled : null;

    /// <summary>Takes the shell's views state.</summary>
    /// <param name="stalled">True while the views are stalled.</param>
    /// <param name="rebuild">The banner's Rebuild views control, or null.</param>
    internal void SetViewsStalled(bool stalled, ControlViewModel? rebuild)
    {
        (ViewsStalled, Rebuild) = (stalled, rebuild);
        OnChanged(nameof(HasRebuild));
        NotifyStalled();
    }

    private void NotifyStalled()
    {
        foreach (var name in new[] { nameof(ShowsEmpty), nameof(ShowsHeaderNew), nameof(ShowsCreateForm), nameof(CanOpenCreate), nameof(NewWorkflowTip) })
        {
            OnChanged(name);
        }
    }
}
