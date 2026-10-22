using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One lane of the Workflows board: its name, its cards and the sentence it reads when empty (F3 workflows spec §2).</summary>
/// <param name="id">The lane.</param>
internal sealed class BoardLaneViewModel(LaneId id) : ObservableObject
{
    /// <summary>The lane.</summary>
    public LaneId Id { get; } = id;

    /// <summary>The lane's name.</summary>
    public string Name { get; } = LineStages.Name(id);

    /// <summary>The cards, in the served order.</summary>
    public ObservableCollection<InstanceRowViewModel> Cards { get; } = [];

    /// <summary>The number of cards, as the header shows it.</summary>
    public string CountText => Cards.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True when the lane holds no card.</summary>
    public bool IsEmpty => Cards.Count == 0;

    /// <summary>True for the Done lane, which is narrower and drawn on the canvas.</summary>
    public bool IsLast => Id == LaneId.Done;

    /// <summary>The empty box's first line; "Nothing here. No runs match this filter." as one sentence while a filter hides the cards.</summary>
    public string EmptyTitle { get; private set => Set(ref field, value); } = Labels.WorkflowsLaneEmpty;

    /// <summary>The empty box's second line.</summary>
    public string EmptyBody { get; private set => Set(ref field, value); } = LineStages.EmptyBody(id);

    /// <summary>What a screen reader hears for the lane: its name and its runs.</summary>
    public string AccessibleName => $"{Name}, {Labels.RunCount(Cards.Count)}";

    /// <summary>Takes the cards, and says why the lane is empty when it is.</summary>
    /// <param name="cards">The cards, in order.</param>
    /// <param name="filtered">True when a filter or a search hides the lane's runs, so the empty box says so.</param>
    internal void Show(IReadOnlyList<InstanceRowViewModel> cards, bool filtered)
    {
        Patch.To(Cards, cards);
        (EmptyTitle, EmptyBody) = filtered ? (Labels.WorkflowsLaneEmpty, Labels.WorkflowsNoRunsMatch) : (Labels.WorkflowsLaneEmpty, LineStages.EmptyBody(Id));
        OnChanged(nameof(CountText));
        OnChanged(nameof(IsEmpty));
        OnChanged(nameof(AccessibleName));
    }
}
