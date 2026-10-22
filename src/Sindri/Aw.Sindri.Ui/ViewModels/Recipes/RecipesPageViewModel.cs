using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>
/// The Recipes page, shown to people as workflows (recipes spec): every template with its standing, filtered All or Needs approval and searched by name or
/// id. Read-only: nothing is written here, and approving is the terminal's or Setup's. The page reads while it is the page shown, and on every event.
/// </summary>
internal sealed partial class RecipesPageViewModel(ISindriApi api) : ObservableObject, IDisposable
{
    private List<RecipeRowViewModel> _all = [];
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;

    /// <summary>Raised when a row asks to open its sheet; the shell opens it.</summary>
    internal event EventHandler<RecipeOpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when "Approve new workflows" is pressed; the shell goes to Setup's held approve-all.</summary>
    internal event EventHandler? ApproveNewRequested;

    /// <summary>The rows shown: the filter and the search applied to the list, in the API's order.</summary>
    public ObservableCollection<RecipeRowViewModel> Rows { get; } = [];

    /// <summary>The selected row (J and K); null before a key moves it.</summary>
    internal RecipeRowViewModel? Selected { get; private set; }

    /// <summary>The page's title: one constant, <see cref="Labels.RecipesPageTitle"/>.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.RecipesPageTitle;

    /// <summary>The column headers.</summary>
    public string ColWorkflow => Labels.ColWorkflow;

    /// <summary>The Version column's header.</summary>
    public string ColVersion => Labels.ColVersion;

    /// <summary>The Standing column's header.</summary>
    public string ColStanding => Labels.ColStanding;

    /// <summary>The Size column's header.</summary>
    public string ColSize => Labels.ColSize;

    /// <summary>The needs-and-produces column's header.</summary>
    public string ColFlow => Labels.ColFlow;

    /// <summary>The Fingerprint column's header.</summary>
    public string ColFingerprint => Labels.ColFingerprint;

    /// <summary>The search field's hint.</summary>
    public string SearchHint => Labels.RecipesSearch;

    /// <summary>The "Approve new workflows" button's words.</summary>
    public string ApproveNewText => Labels.ApproveNewWorkflows;

    /// <summary>"Reading workflows…".</summary>
    public string ReadingText => Labels.ReadingRecipes;

    /// <summary>The box for an empty list.</summary>
    public string EmptyText => Labels.RecipesEmpty;

    /// <summary>The box for a filter that matches nothing.</summary>
    public string NoMatchText => Labels.RecipesNoMatch;

    /// <summary>The line under the table.</summary>
    public string FooterText => Labels.RecipesFooter;

    /// <summary>What the page says when an answer can't be read.</summary>
    public string UnreadableText => Labels.RecipeUnreadable;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.TryAgain;
#pragma warning restore CA1822, MA0041

    /// <summary>"12 workflows · 11 approved"; empty until the first list.</summary>
    public string Caption { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The All filter's label with its count.</summary>
    public string AllText { get; private set => Set(ref field, value); } = Labels.RecipesFilterAll;

    /// <summary>The Needs approval filter's label with its count.</summary>
    public string NeedsText { get; private set => Set(ref field, value); } = Labels.RecipesFilterNeeds;

    /// <summary>True while All is the filter.</summary>
    public bool IsAllFilter => !NeedsOnly;

    /// <summary>True while Needs approval is the filter.</summary>
    public bool IsNeedsFilter => NeedsOnly;

    private bool NeedsOnly { get; set; }

    /// <summary>The search text; setting it narrows the rows at once.</summary>
    public string Query
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value ?? string.Empty;
            OnChanged(nameof(Query));
            Apply();
        }
    } = string.Empty;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the list has no workflows at all.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>True when there are workflows but the filter and search show none.</summary>
    public bool IsNoMatch { get; private set => Set(ref field, value); }

    /// <summary>True when the table shows (a list was read and is not empty).</summary>
    public bool IsTableShown { get; private set => Set(ref field, value); }

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the rows shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the first read failed and there are no rows to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed first read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Shows every workflow.</summary>
    internal void ShowAll() => SetFilter(needsOnly: false);

    /// <summary>Shows the workflows that need approval.</summary>
    internal void ShowNeeds() => SetFilter(needsOnly: true);

    /// <summary>Asks the shell to open a workflow's sheet.</summary>
    /// <param name="link">The recipe's link.</param>
    internal void RequestOpen(DeepLink link) => OpenRequested?.Invoke(this, new RecipeOpenRequestedEventArgs(link));

    /// <summary>Asks the shell to go to Setup (never approves here).</summary>
    internal void RequestApproveNew() => ApproveNewRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The row listed for a recipe id, or null.</summary>
    /// <param name="id">The recipe id.</param>
    /// <returns>The first row with that id.</returns>
    internal RecipeRowViewModel? Find(string id) => _all.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    private void SetFilter(bool needsOnly)
    {
        NeedsOnly = needsOnly;
        OnChanged(nameof(IsAllFilter));
        OnChanged(nameof(IsNeedsFilter));
        Apply();
    }

    private void Apply()
    {
        var shown = _all.Where(r => (!NeedsOnly || r.NeedsApproval) && r.Matches(Query)).ToList();
        Patch.To(Rows, shown);
        if (Selected is { } selected && !shown.Contains(selected))
        {
            Mark(row: null);
        }

        IsNoMatch = _all.Count > 0 && shown.Count == 0;
        IsTableShown = shown.Count > 0;
    }

    private void Show(RecipeList list)
    {
        Drawn++;
        (IsReading, IsUnreadable, IsStale, IsFailed, FailedText) = (false, false, false, false, string.Empty);
        var kept = _all.ToDictionary(r => r.Signature, StringComparer.Ordinal);
        _all = [.. list.Recipes.Select(r => new RecipeRowViewModel(r)).Select(r => kept.GetValueOrDefault(r.Signature, r))];
        Caption = Labels.RecipesCaption(_all.Count, _all.Count(r => r.IsApproved));
        AllText = Labels.RecipesFilterWithCount(Labels.RecipesFilterAll, _all.Count);
        NeedsText = Labels.RecipesFilterWithCount(Labels.RecipesFilterNeeds, _all.Count(r => r.NeedsApproval));
        IsEmpty = _all.Count == 0;
        if (Selected is { } selected)
        {
            var again = _all.Find(r => string.Equals(r.Id, selected.Id, StringComparison.Ordinal));
            Mark(again);
        }

        Apply();
    }

    private void Fail(Error error)
    {
        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        (IsReading, IsUnreadable, IsStale) = (false, unreadable, true);   // the last good rows stay, marked stale (screens §4.1)
        IsFailed = !unreadable && _all.Count == 0;   // with nothing to show, the page says so and offers to try again
        FailedText = IsFailed ? Labels.RecipesFailed(error) : string.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }
}
