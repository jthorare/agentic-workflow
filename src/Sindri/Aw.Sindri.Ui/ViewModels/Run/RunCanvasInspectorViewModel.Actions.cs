using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The inspector's actions (<see cref="RunCanvasInspectorViewModel"/>, continued): all of them navigate; none sends a command.</summary>
internal sealed partial class RunCanvasInspectorViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The why block: nothing writes it down yet, so it says so.</summary>
    public string WhyText => Labels.RunWhyNotServed;

    /// <summary>The line that says the facts are the stage's.</summary>
    public string FactsHeading => Labels.RunFactsWholeStage;

    /// <summary>The next actions heading.</summary>
    public string NextHeading => Labels.RunNext;

    /// <summary>The why block's heading.</summary>
    public string WhyHeading => Labels.RunWhy;

    /// <summary>The timeline's heading.</summary>
    public string TimelineHeading => Labels.RunTimeline;

    /// <summary>The needs heading.</summary>
    public string NeedsHeading => Labels.RunNeeds;

    /// <summary>The produced heading.</summary>
    public string ProducedHeading => Labels.RunProduced;

    /// <summary>The technical details' heading.</summary>
    public string TechnicalHeading => Labels.RunTechnical;

    /// <summary>The raw output's heading.</summary>
    public string RawHeading => Labels.RunRawOutput;

    /// <summary>See its context's words.</summary>
    public string SeeContextLabel => Labels.RunSeeContext;

    /// <summary>See its context's help.</summary>
    public string SeeContextHelp => Labels.RunSeeContextHelp;

    /// <summary>The close button's words.</summary>
    public string CloseLabel => Labels.RunInspectorClose;

    /// <summary>The previous button's words.</summary>
    public string PreviousLabel => Labels.RunInspectorPrevious;

    /// <summary>The next button's words.</summary>
    public string NextLabel => Labels.RunInspectorNext;
#pragma warning restore CA1822, MA0041

    /// <summary>The take-over button's words: Take over, Hand back or Decide in Needs you, by where the step is; empty when the step offers none.</summary>
    public string TakeOverLabel => State switch
    {
        RunNodeState.Running => Labels.RunTakeOver,
        RunNodeState.TookOver => Labels.RunHandBack,
        RunNodeState.Waiting or RunNodeState.Paused => Labels.RunDecide,
        RunNodeState.NotStarted or RunNodeState.Done or RunNodeState.Failed or RunNodeState.DidntWorkOut or RunNodeState.Cancelled => string.Empty,
        _ => string.Empty,
    };

    /// <summary>True when the take-over button is drawn: the step belongs to an instance and is one a person can act on.</summary>
    public bool HasTakeOver => _owner is not null && TakeOverLabel.Length > 0;

    /// <summary>The take-over block's title; empty unless the button is Take over.</summary>
    public string TakeOverTitle => State == RunNodeState.Running ? Labels.RunTakeOverTitle : string.Empty;

    /// <summary>The take-over block's help; empty unless the button is Take over.</summary>
    public string TakeOverHelp => State == RunNodeState.Running ? Labels.RunTakeOverHelp : string.Empty;

    /// <summary>True when See its context is drawn: the run belongs to an item.</summary>
    public bool HasContext => _line.ItemId is not null;

    /// <summary>True when any action is drawn.</summary>
    public bool HasActions => HasTakeOver || HasContext || Protected is not null;

    /// <summary>Opens the owning instance's view in Needs you (Take over, Hand back, Decide). Sends nothing: the takeover itself is made there.</summary>
    public void TakeOver()
    {
        if (_owner is { } owner)
        {
            _ports.Go(new DeepLink(DeepLinkKind.NeedsYou, owner.Id));
        }
    }

    /// <summary>Opens the item's map in Context.</summary>
    public void SeeContext()
    {
        if (_line.ItemId is { } item)
        {
            _ports.Go(new DeepLink(DeepLinkKind.Node, item));
        }
    }

    /// <summary>Clears the selection.</summary>
    public void Close() => _ports.Close();

    /// <summary>Moves to the next step in reading order.</summary>
    public void Next() => _ports.Walk(1);

    /// <summary>Moves to the previous step in reading order.</summary>
    public void Previous() => _ports.Walk(-1);
}
