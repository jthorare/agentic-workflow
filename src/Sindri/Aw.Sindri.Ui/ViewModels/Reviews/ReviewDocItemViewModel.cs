namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One item of the doc map (review page spec §4.4, §6.2): a phase or requirement with who claims it.</summary>
internal sealed class ReviewDocItemViewModel : ObservableObject
{
    /// <summary>The item's id (Ph5, REQ-E3).</summary>
    public string Id { get; } = string.Empty;

    /// <summary>Its title.</summary>
    public string Title { get; } = string.Empty;

    /// <summary>The mark on the right: "this PR", "claimed by #2060, merged" or "unclaimed"; empty for none.</summary>
    public string Mark { get; } = string.Empty;

    /// <summary>True for an item this PR claims (drawn highlighted).</summary>
    public bool IsClaimedHere { get; }

    /// <summary>True when Send as fix task is offered (an unclaimed requirement).</summary>
    public bool CanSendFix { get; }

    /// <summary>The screen-reader name.</summary>
    public string AutomationName { get; } = string.Empty;

    /// <summary>The item's children.</summary>
    public IReadOnlyList<ReviewDocItemViewModel> Children { get; } = [];
}
