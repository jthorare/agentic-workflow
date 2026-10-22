using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One card of the runs sidebar (F3 spec 3 §2, "RUNS sidebar"): a run of the same workflow, with its state word and how long it has run.</summary>
internal sealed class RunListItemViewModel : ObservableObject
{
    internal RunListItemViewModel(InstanceSummary row, DateTimeOffset now)
    {
        Address = row.Address;
        Id = row.Id;
        Title = row.Subject.Label.Length > 0 ? row.Subject.Label : row.Subject.Key;
        State = RunStates.FromChip(row.Chip);
        Word = RunStates.Word(State);
        Icon = RunStateIcons.Of(State);
        TextBrush = RunStates.Brushes(State).Text;
        Elapsed = Labels.Duration((long)Math.Max(0, (now - (row.StartedAt ?? row.Since)).TotalSeconds));
        AutomationName = Labels.RunCardName(Title, Word);
        // A run sitting at a stop or with you is not on the canvas: its card leads to Needs you (owner answer Q3).
        OpensNeedsYou = State is RunNodeState.Waiting or RunNodeState.Paused or RunNodeState.TookOver;
        Link = OpensNeedsYou ? DeepLink.HomeNeedsYou : new DeepLink(DeepLinkKind.Workflow, row.Address);
    }

    /// <summary>The run's address.</summary>
    public string Address { get; }

    /// <summary>The run's instance id.</summary>
    public string Id { get; }

    /// <summary>The run's subject, as served.</summary>
    public string Title { get; }

    /// <summary>Where the run is.</summary>
    public RunNodeState State { get; }

    /// <summary>The state's word.</summary>
    public string Word { get; }

    /// <summary>The state's mark (an Icons.axaml name).</summary>
    public string Icon { get; }

    /// <summary>The brush key of the glyph and the word.</summary>
    public string TextBrush { get; }

    /// <summary>How long the run has been going.</summary>
    public string Elapsed { get; }

    /// <summary>The card's accessible name.</summary>
    public string AutomationName { get; }

    /// <summary>True when the card opens Needs you instead of the canvas.</summary>
    public bool OpensNeedsYou { get; }

    /// <summary>Where a click goes.</summary>
    public DeepLink Link { get; }

    /// <summary>True while the card is the run on the page.</summary>
    public bool IsCurrent { get; internal set => Set(ref field, value); }
}
