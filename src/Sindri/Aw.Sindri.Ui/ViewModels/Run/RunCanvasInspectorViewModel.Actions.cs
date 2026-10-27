using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The inspector's actions (<see cref="RunCanvasInspectorViewModel"/>, continued): Take over on a running step sends the takeover; the others navigate.</summary>
internal sealed partial class RunCanvasInspectorViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The why block: the stage's served why, else the words that say nothing writes it down.</summary>
    public string WhyText => string.IsNullOrWhiteSpace(Why) ? Labels.RunWhyNotServed : Why;

    /// <summary>The stage's why as the recipe serves it; null or blank when it serves none.</summary>
    internal string? Why { get; init; }

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

    /// <summary>Take over on a running step sends "ApplyCommand:takeover" for its instance, once per press, and says what came of it; Hand back and Decide open the instance in Needs you, where they are made.</summary>
    public void TakeOver()
    {
        if (_owner is not { } owner)
        {
            return;
        }

        if (State != RunNodeState.Running)
        {
            _ports.Go(new DeepLink(DeepLinkKind.NeedsYou, owner.Id));
        }
        else if (Sender is { } sender)
        {
            _ = sender.Press(owner.Id, owner.Current.Status.Run?.Launch, Say);
        }
        else
        {
            Say(new TakeoverView(Labels.TakeoverNotSent, string.Empty));
        }
    }

    private void Say(TakeoverView view) => (TakeOverLine, TakeOverCommand) = (view.Line, view.Command);

    /// <summary>What the last Take over came to, in words ("Sending…", the viewer's outcome, or the refusal with its code); empty before one.</summary>
    public string TakeOverLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The attach command to copy when no terminal opened; empty otherwise.</summary>
    public string TakeOverCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Sends the takeover; set by the page, null when nothing can be sent.</summary>
    internal TakeoverSender? Sender { get; init; }

    /// <summary>Opens the item's map in Context.</summary>
    public void SeeContext()
    {
        if (_line.ItemId is { } item)
        {
            _ports.Go(new DeepLink(DeepLinkKind.ContextItem, item));
        }
    }

    /// <summary>Clears the selection.</summary>
    public void Close() => _ports.Close();

    /// <summary>Moves to the next step in reading order.</summary>
    public void Next() => _ports.Walk(1);

    /// <summary>Moves to the previous step in reading order.</summary>
    public void Previous() => _ports.Walk(-1);
}
