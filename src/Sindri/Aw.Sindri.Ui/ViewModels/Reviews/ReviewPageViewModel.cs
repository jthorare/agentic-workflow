using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// One PR's review page (review page spec §6.2-6.4, §16): the header, the summary strip, the panels in settings order and the diff, in every state of §6.3.
/// Release 1 posts nothing: the review buttons open GitHub.
/// </summary>
internal sealed partial class ReviewPageViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly ReviewPanelMemory _memory;
    private Uri? _url;
    private string _repo = string.Empty;
    private int _number;
    private int _reads;
    private string _settings = string.Empty;

    internal ReviewPageViewModel(ISindriApi api, ReviewPanelMemory? memory = null)
    {
        (_api, _memory) = (api, memory ?? new ReviewPanelMemory());
        Login = new ReviewLoginViewModel(api);
        Diff = new ReviewDiffViewModel();
        ShowLoading();
    }

    /// <summary>What the page shows: loading, the page, or one of the whole-page refusals.</summary>
    public ReviewPageState State
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsPage));
            OnChanged(nameof(ShowsState));
            OnChanged(nameof(IsLoginPrompt));
        }
    } = ReviewPageState.Loading;

    /// <summary>True while the page (or its placeholders) shows: loading or ready.</summary>
    public bool ShowsPage => State is ReviewPageState.Loading or ReviewPageState.Ready;

    /// <summary>True when a whole-page refusal shows instead.</summary>
    public bool ShowsState => !ShowsPage;

    /// <summary>True when the whole page is the login prompt.</summary>
    public bool IsLoginPrompt => State == ReviewPageState.LoginPrompt;

    /// <summary>The login prompt's field and Save.</summary>
    public ReviewLoginViewModel Login { get; }

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

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>"Release 1: both buttons open this PR's review box on GitHub. Your ticks are saved on this head."</summary>
    public string ActionNote => Labels.ReviewActionNote;
#pragma warning restore CA1822, MA0041

    /// <summary>The summary strip: four placeholders while loading, then the served parts.</summary>
    public ObservableCollection<ReviewStripSegmentViewModel> Strip { get; } = [];

    /// <summary>The panels, in the order the settings give.</summary>
    public ObservableCollection<ReviewPanelViewModel> Panels { get; } = [];

    /// <summary>The diff.</summary>
    public ReviewDiffViewModel Diff { get; }

    /// <summary>True at 1,280 px and wider: the panel column beside the diff, every panel open. Below, the panels stack above the diff, collapsed except required checks.</summary>
    public bool IsWide
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            foreach (var panel in Panels.Where(p => _memory.Chosen(_settings, p.Id) is null))
            {
                panel.SetDefaultCollapsed(!(value || string.Equals(panel.Id, "requiredChecks", StringComparison.Ordinal)));
            }

            OnChanged(nameof(IsWide));
        }
    }

    /// <summary>True when Approve on GitHub can be pressed (not on the owner's own PR).</summary>
    public bool ApproveEnabled { get; private set => Set(ref field, value); }

    /// <summary>True when Request changes on GitHub can be pressed (not on the owner's own PR).</summary>
    public bool RequestChangesEnabled { get; private set => Set(ref field, value); }

    /// <summary>"You can't approve or request changes on your own PR: GitHub takes a comment only" on the owner's own PR; empty otherwise.</summary>
    public string OwnPrText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the review buttons go: the PR's review box on GitHub.</summary>
    public string ReviewUrl { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where focus goes next (review page spec §6.4 "Focus after"); the view moves it there.</summary>
    public ReviewFocus Focus
    {
        get;
        private set
        {
            field = value;
            OnChanged(nameof(Focus));   // always: asking for the same place twice moves focus twice
        }
    } = ReviewFocus.None;

    /// <summary>Reads the PR's page; the page is loading until the answer.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Open(Uri url, string repo, int number, CancellationToken cancellationToken)
    {
        (_url, _repo, _number) = (url, repo, number);
        ShowLoading();
        return Read(cancellationToken);
    }

    /// <summary>Reads the page again (Try again, Reload); focus lands on the summary strip's first part.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task Reload(CancellationToken cancellationToken)
    {
        await Read(cancellationToken).ConfigureAwait(true);
        Focus = new ReviewFocus(ReviewFocusRegion.StripFirst);
    }

    /// <summary>The back button: the shell shows the Reviews list again.</summary>
    internal void GoBack() => BackRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Back was pressed.</summary>
    internal event EventHandler? BackRequested;

    /// <summary>A strip part was pressed: its panel opens and focus goes to it.</summary>
    /// <param name="panel">The panel id.</param>
    internal void Jump(string panel)
    {
        if (Panels.FirstOrDefault(p => string.Equals(p.Id, panel, StringComparison.Ordinal)) is not { } target)
        {
            return;
        }

        target.IsCollapsed = false;
        Focus = new ReviewFocus(ReviewFocusRegion.Panel, panel);
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var read = ++_reads;
        var answer = await _api.ReadReviewPage(url, _repo, _number, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for: its answer is the one to show
        }

        if (answer.IsSuccess)
        {
            Show(answer.Value);
            return;
        }

        Fail(answer.Error);
    }

    private void Fail(Kernel.Error error)
    {
        Strip.Clear();
        Panels.Clear();
        (StateHeading, StateText) = error.Code switch
        {
            "SND-REVIEW-404" => (Labels.ReviewNotFoundHeading, error.Message),
            "SND-REVIEW-004" => (Labels.ReviewNotSyncedHeading, error.Message),
            "SND-REVIEW-007" => (Labels.ReviewLoginHeading, error.Message),
            "SND-REVIEW-503" => (Labels.ReviewOfflineHeading, error.Message),
            _ => (string.Empty, Labels.InstanceRefused(error)),
        };
        State = error.Code switch
        {
            "SND-REVIEW-404" => ReviewPageState.NotFound,
            "SND-REVIEW-004" => ReviewPageState.NotSynced,
            "SND-REVIEW-007" => ReviewPageState.LoginPrompt,
            "SND-REVIEW-503" => ReviewPageState.OfflineUncached,
            _ => ReviewPageState.Failed,
        };
    }

    private void ShowLoading()
    {
        State = ReviewPageState.Loading;
        (StateHeading, StateText) = (string.Empty, string.Empty);
        ClearHeader();
        Patch(Strip, [
            new ReviewStripSegmentViewModel(string.Empty, Labels.ReviewStripReadingChecks, StateGlyph.None, isLoading: true),
            new ReviewStripSegmentViewModel(string.Empty, Labels.ReviewStripReadingClaim, StateGlyph.None, isLoading: true),
            new ReviewStripSegmentViewModel(string.Empty, Labels.ReviewStripReadingRisk, StateGlyph.None, isLoading: true),
            new ReviewStripSegmentViewModel(string.Empty, Labels.ReviewStripReadingCi, StateGlyph.None, isLoading: true),
        ]);
        Patch(Panels, [.. _defaultOrder.Select(id => new ReviewPanelViewModel(id, ReviewPanelState.Loading, Labels.ReviewPanelReading, string.Empty, collapsed: false))]);
        Diff.ShowLoading();
    }

    private static readonly string[] _defaultOrder = ["requiredChecks", "docMap", "testPlan", "ci", "evidence", "risk", "conversation"];

    private static void Patch<T>(ObservableCollection<T> to, IEnumerable<T> items)
    {
        to.Clear();
        foreach (var item in items)
        {
            to.Add(item);
        }
    }

    private void Show(Api.Reviews.ReviewPage page)
    {
        var pr = page.Pr;
        State = ReviewPageState.Ready;
        (StateHeading, StateText) = (string.Empty, string.Empty);
        (Title, Author, HeadText, Branch, PrUrl) = (pr.Title, pr.Author, Labels.ReviewHead(pr.Head.Sha7), pr.Head.Branch, pr.Url);
        Ref = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{pr.Repo}#{pr.Number}");
        IssueKeys = [.. pr.Issues.Select(i => i.Key)];
        (IsDraft, IsYours) = (pr.Draft, pr.Yours);
        _settings = page.Settings?.Name ?? string.Empty;
        SettingsText = page.Settings is { } settings ? Labels.ReviewSettings(settings.Name) : string.Empty;
        (OfflineText, MovingText) = (page.Offline?.Text, page.Moving?.Text);
        (ApproveEnabled, RequestChangesEnabled) = (!pr.Yours, !pr.Yours);
        OwnPrText = pr.Yours ? Labels.ReviewOwnPr : string.Empty;
        ReviewUrl = pr.Url + "/files";
        FixTarget = new ReviewFixTarget(pr.Repo, pr.Number, pr.Head.Sha, pr.Head.Sha7, pr.Head.Branch, pr.Author, pr.PostsFixThreads, DocIdOf(page));
        ShowPanels(page);
        Diff.Show(page.Diff);
    }

    private static string DocIdOf(Api.Reviews.ReviewPage page) =>
        page.Panels.FirstOrDefault(p => string.Equals(p.Id, "docMap", StringComparison.Ordinal))?.Data.Doc?.Id ?? string.Empty;
}
