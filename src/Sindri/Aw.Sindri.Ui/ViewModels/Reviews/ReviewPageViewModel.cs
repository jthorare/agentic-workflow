using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// One PR's review page (review page spec §6.2-6.4, §16): the header, the summary strip, the panels in settings order and the diff, in every state of §6.3.
/// Release 1 posts nothing: the review buttons open GitHub.
/// </summary>
internal sealed class ReviewPageViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly ReviewPanelMemory _memory;

    internal ReviewPageViewModel(ISindriApi api, TimeProvider clock, ReviewPanelMemory? memory = null)
    {
        (_api, _clock, _memory) = (api, clock, memory ?? new ReviewPanelMemory());
        Diff = new ReviewDiffViewModel();
    }

    /// <summary>What the page shows: loading, the page, or one of the whole-page refusals.</summary>
    public ReviewPageState State { get; private set => Set(ref field, value); } = ReviewPageState.Loading;

    /// <summary>The heading of a whole-page state ("Pull request not found"); empty for the page.</summary>
    public string StateHeading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The words under it: the refusal's detail as served; empty for the page.</summary>
    public string StateText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The PR's title.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"acme/api#2071".</summary>
    public string Ref { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The author's login.</summary>
    public string Author { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"head a1b2c3d".</summary>
    public string HeadText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The head branch.</summary>
    public string Branch { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The PR on GitHub.</summary>
    public string PrUrl { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Settings: sindri"; empty when no settings apply.</summary>
    public string SettingsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Linear issue keys, plain text until the Linear sheet exists.</summary>
    public IReadOnlyList<string> IssueKeys { get; private set => Set(ref field, value); } = [];

    /// <summary>True for a draft PR.</summary>
    public bool IsDraft { get; private set => Set(ref field, value); }

    /// <summary>True when the PR is the owner's own.</summary>
    public bool IsYours { get; private set => Set(ref field, value); }

    /// <summary>"as of 23:45, the PR may have moved" when the page is the cached one; null otherwise.</summary>
    public string? OfflineText { get; private set => Set(ref field, value); }

    /// <summary>"The PR is moving; reload when it settles." when the head moved twice during the read; null otherwise.</summary>
    public string? MovingText { get; private set => Set(ref field, value); }

    /// <summary>The summary strip: four placeholders while loading, then the served parts.</summary>
    public ObservableCollection<ReviewStripSegmentViewModel> Strip { get; } = [];

    /// <summary>The panels, in the order the settings give.</summary>
    public ObservableCollection<ReviewPanelViewModel> Panels { get; } = [];

    /// <summary>The diff.</summary>
    public ReviewDiffViewModel Diff { get; }

    /// <summary>True at 1,280 px and wider: the panel column beside the diff, every panel open. Below, the panels stack above the diff, collapsed except required checks.</summary>
    public bool IsWide { get; set; }

    /// <summary>True when Approve on GitHub can be pressed (not on the owner's own PR).</summary>
    public bool ApproveEnabled { get; private set => Set(ref field, value); }

    /// <summary>True when Request changes on GitHub can be pressed (not on the owner's own PR).</summary>
    public bool RequestChangesEnabled { get; private set => Set(ref field, value); }

    /// <summary>"You can't approve or request changes on your own PR: GitHub takes a comment only" on the owner's own PR; empty otherwise.</summary>
    public string OwnPrText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the review buttons go: the PR's review box on GitHub.</summary>
    public string ReviewUrl { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where focus goes next (review page spec §6.4 "Focus after"); the view moves it there.</summary>
    public ReviewFocus Focus { get; private set => Set(ref field, value); } = ReviewFocus.None;

    /// <summary>Reads the PR's page; the page is loading until the answer.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Open(Uri url, string repo, int number, CancellationToken cancellationToken)
    {
        _ = (url, repo, number, cancellationToken, _api, _clock, _memory);
        return Task.CompletedTask;
    }

    /// <summary>Reads the page again (Try again, Reload); focus lands on the summary strip's first part.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Reload(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Focus = ReviewFocus.None;
        return Task.CompletedTask;
    }

    /// <summary>A strip part was pressed: its panel opens and focus goes to it.</summary>
    /// <param name="panel">The panel id.</param>
    internal void Jump(string panel)
    {
        _ = panel;
        Focus = ReviewFocus.None;
    }
}
