using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One item of the doc map (review page spec §4.4, §6.2): a phase or requirement with who claims it.</summary>
internal sealed class ReviewDocItemViewModel : ObservableObject
{
    internal ReviewDocItemViewModel(ReviewDocItemView item, int depth = 0)
    {
        Indent = new string(' ', depth * 3);
        (Id, Title, IsClaimedHere, CanSendFix) = (item.Id, item.Title, item.ClaimedHere, item.FixTask);
        Mark = item.ClaimedHere ? Labels.ReviewDocThisPr : item.ClaimedBy.Count > 0 ? string.Join("; ", item.ClaimedBy.Select(c => c.Text)) : item.Unclaimed ? Labels.ReviewDocUnclaimed : string.Empty;
        AutomationName = Mark.Length > 0 ? $"{Id}, {Title}, {Mark}" : $"{Id}, {Title}";
        Children = [.. item.Items.Select(i => new ReviewDocItemViewModel(i, depth + 1))];
    }

    /// <summary>Spaces that indent a child under its parent.</summary>
    public string Indent { get; }

    /// <summary>The item's id (Ph5, REQ-E3).</summary>
    public string Id { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>The mark on the right: "this PR", "claimed by #2060, merged" or "unclaimed"; empty for none.</summary>
    public string Mark { get; }

    /// <summary>True for an item this PR claims (drawn highlighted).</summary>
    public bool IsClaimedHere { get; }

    /// <summary>True when Send as fix task is offered (an unclaimed requirement).</summary>
    public bool CanSendFix { get; }

    /// <summary>The screen-reader name.</summary>
    public string AutomationName { get; }

    /// <summary>The item's children.</summary>
    public IReadOnlyList<ReviewDocItemViewModel> Children { get; }
}
