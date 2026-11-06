namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One panel of the review page (review page spec §6.2, §6.3): its title, state, words, rows, and whether it is collapsed.</summary>
internal sealed class ReviewPanelViewModel : ObservableObject
{
    private readonly Action<ReviewPanelViewModel, bool>? _chosen;
    private readonly bool _tracking;

    internal ReviewPanelViewModel(string id, ReviewPanelState state, string text, string summary, bool collapsed, Action<ReviewPanelViewModel, bool>? chosen = null)
    {
        (Id, Title, State, Text, Summary) = (id, TitleOf(id), state, text, summary);
        IsCollapsed = collapsed;   // the layout's default is not the owner's choice: nothing is kept until the ctor is done
        _chosen = chosen;
        _tracking = true;
    }

    /// <summary>requiredChecks, docMap, testPlan, ci, evidence, risk or conversation.</summary>
    public string Id { get; }

    /// <summary>The panel's title ("Required checks").</summary>
    public string Title { get; }

    /// <summary>Loading, ready, empty or error.</summary>
    public ReviewPanelState State { get; }

    /// <summary>The words of a loading, empty or error panel; empty when ready.</summary>
    public string Text { get; }

    /// <summary>The header's summary: the strip's words for the panel.</summary>
    public string Summary { get; }

    /// <summary>True when collapsed. Setting it keeps the choice per settings name.</summary>
    public bool IsCollapsed
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(IsCollapsed));
            OnChanged(nameof(IsOpen));
            if (_tracking)
            {
                _chosen?.Invoke(this, value);
            }
        }
    }

    /// <summary>True when open (the body shows).</summary>
    public bool IsOpen => !IsCollapsed;

    /// <summary>The required checks, for the requiredChecks panel.</summary>
    public IReadOnlyList<ReviewCheckRowViewModel> Checks { get; internal init; } = [];

    /// <summary>The doc map's items, for the docMap panel.</summary>
    public IReadOnlyList<ReviewDocItemViewModel> DocItems { get; internal init; } = [];

    /// <summary>The claim line of the docMap panel ("Covers Ph5, REQ-E3"); empty when none.</summary>
    public string ClaimsText { get; internal init; } = string.Empty;

    /// <summary>The plain lines of the testPlan, ci, evidence, risk and conversation panels.</summary>
    public IReadOnlyList<ReviewPanelLine> Lines { get; internal init; } = [];

    /// <summary>True when the panel has rows to draw.</summary>
    public bool IsReady => State == ReviewPanelState.Ready;

    /// <summary>The panel's title by id.</summary>
    /// <param name="id">The panel id.</param>
    /// <returns>The title.</returns>
    internal static string TitleOf(string id) => id switch
    {
        "requiredChecks" => Labels.ReviewPanelRequiredChecks,
        "docMap" => Labels.ReviewPanelDocMap,
        "testPlan" => Labels.ReviewPanelTestPlan,
        "ci" => Labels.ReviewPanelCi,
        "evidence" => Labels.ReviewPanelEvidence,
        "risk" => Labels.ReviewPanelRisk,
        "conversation" => Labels.ReviewPanelConversation,
        _ => id,
    };
}
