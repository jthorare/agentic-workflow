using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// The Reviews tab (review page spec §6.1, §6.3, §16): Waiting on you, then Yours, the library header, the login prompt and the waiting count behind the tab's badge and Home's line.
/// It reads on every event even when it isn't the page shown, since the badge and Home's line need the count from any page.
/// </summary>
internal sealed partial class ReviewsPageViewModel : ObservableObject, IDisposable
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;

    internal ReviewsPageViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
        Library = new LibrarySheetViewModel(api);
    }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"Reviews".</summary>
    public string Title => Labels.ReviewsPageTitle;

    /// <summary>"Reading your PRs…" until the first answer.</summary>
    public string ReadingText => Labels.ReadingReviews;

    /// <summary>The login prompt's Save button.</summary>
    public string SaveText => Labels.ReviewsLoginSave;

    /// <summary>The login field's name.</summary>
    public string LoginFieldName => Labels.ReviewsLoginFieldName;

    /// <summary>The hint line under the list.</summary>
    public string HintText => Labels.ReviewsHint;

#pragma warning restore CA1822, MA0041

    /// <summary>True until the first answer.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The line under the title: "GitHub synced 3 min ago · reviews as joi-example · 4 open PRs in 2 repos".</summary>
    public string SubLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The library header's words; its button opens the library errors sheet.</summary>
    public string LibraryText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanOpenLibrary));
        }
    } = string.Empty;

    /// <summary>True when the header shows a warning (docs with errors, or a folder that can't be read).</summary>
    public bool LibraryWarns { get; private set => Set(ref field, value); }

    /// <summary>The served notice above the list; null for none.</summary>
    public string? Notice { get; private set => Set(ref field, value); }

    /// <summary>"PR list is from …" above the last rows; null when fresh.</summary>
    public string? StaleText { get; private set => Set(ref field, value); }

    /// <summary>True while review.login is unset: the page is the one-field prompt.</summary>
    public bool IsLoginPrompt { get; private set => Set(ref field, value); }

    /// <summary>The prompt's heading ("Set your GitHub login").</summary>
    public string LoginHeading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The login field, pre-filled with the served suggestion.</summary>
    public string LoginField
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSaveLogin));
        }
    } = string.Empty;

    /// <summary>The empty state's lines (text, hint, teams); null when there are rows.</summary>
    public IReadOnlyList<string>? EmptyLines { get; private set => Set(ref field, value); }

    /// <summary>Waiting on you, then Yours.</summary>
    public ObservableCollection<ReviewGroupViewModel> Groups { get; } = [];

    /// <summary>The Waiting on you group's size: the tab's badge and Home's line.</summary>
    public int WaitingCount { get; private set => Set(ref field, value); }

    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The login prompt's failure under the field (the server's detail); empty when there is none.</summary>
    public string LoginError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when Save can be pressed: the field has text.</summary>
    public bool CanSaveLogin => !string.IsNullOrWhiteSpace(LoginField);

    /// <summary>The column headings, in order.</summary>
    public IReadOnlyList<string> Columns { get; } = [Labels.ReviewsColPr, Labels.ReviewsColAuthor, Labels.ReviewsColAge, Labels.ReviewsColCi, Labels.ReviewsColRequired, Labels.ReviewsColDoc];

    /// <summary>True when the header's library button can be pressed.</summary>
    public bool CanOpenLibrary => LibraryText.Length > 0;

    /// <summary>The library errors sheet.</summary>
    public LibrarySheetViewModel Library { get; }

    /// <summary>Saves the login in the field, then reads the list again.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task SaveLogin(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanSaveLogin)
        {
            return;
        }

        var saved = await _api.SaveReviewLogin(url, new ReviewLoginSave(LoginField.Trim()), cancellationToken).ConfigureAwait(true);
        if (saved.IsError)
        {
            LoginError = Labels.InstanceRefused(saved.Error);
            return;
        }

        LoginError = string.Empty;
        await Read(url, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>The header's library button: opens the errors sheet and reads the library.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the sheet shows the answer.</returns>
    internal Task OpenLibrary(CancellationToken cancellationToken)
    {
        LibraryRequested?.Invoke(this, EventArgs.Empty);
        return _url is { } url ? Library.Open(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The header's library button was pressed: the shell opens its sheet.</summary>
    internal event EventHandler? LibraryRequested;

    /// <summary>Enter on a row (or a click): asks the shell to open the PR's review page.</summary>
    /// <param name="row">The row.</param>
    internal void OpenRow(ReviewRowViewModel row) => OpenRequested?.Invoke(this, new Workflows.OpenRequestedEventArgs(Navigation.DeepLink.ForReview(row.Repo, row.Number)));

    /// <summary>A row asked for its review page: the link is 'reviews/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;'.</summary>
    internal event EventHandler<Workflows.OpenRequestedEventArgs>? OpenRequested;

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the list (on connect and on every event), whichever page is shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Read(url, cancellationToken);
    }

    /// <summary>The page was shown or hidden; becoming the page shown reads at once.</summary>
    /// <param name="shown">True while Reviews is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            Dispose();
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            Dispose();
        }
    }

    /// <summary>Cancels the read started when the page was shown.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await _api.Reviews(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        IsLoading = false;   // the last good rows stay under the line
        Error = Labels.ReviewsFailed(error);
    }

    private void Show(ReviewList list)
    {
        (IsLoading, Error) = (false, string.Empty);
        var now = _clock.GetUtcNow();
        var shown = ShowGroups(list, now);
        ShowHeader(list, shown.Count);
        (Notice, StaleText, LibraryText, WaitingCount) = (list.Notice, list.Stale?.Text, list.Library.Text, list.WaitingCount);
        LibraryWarns = list.Library.WithErrors > 0 || list.Library.UnreadableRoots > 0;
        (IsLoginPrompt, LoginHeading) = (list.LoginPrompt is not null, list.LoginPrompt?.Text ?? string.Empty);
        if (list.LoginPrompt is { } prompt && string.IsNullOrEmpty(LoginField))
        {
            LoginField = prompt.Suggested ?? string.Empty;   // only into an empty field: what the owner typed stays
        }

        EmptyLines = list.Empty is { } empty ? [empty.Text, empty.Hint, empty.Teams] : null;
        Patch.To(Groups, shown);
    }

    // "GitHub synced 14 min ago · reviews as joi-example · 4 open PRs in 2 repos": the parts that exist.
    private void ShowHeader(ReviewList list, int groupCount)
    {
        var rows = list.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).ToList();
        var parts = new List<string>();
        if (list.Freshness.FirstOrDefault(f => string.Equals(f.Source, "github", StringComparison.Ordinal)) is { } github)
        {
            parts.Add(Labels.ReviewsSource(github.Text));
        }

        if (list.Login is { } login)
        {
            parts.Add(Labels.ReviewsAs(login));
            if (groupCount > 0)
            {
                parts.Add(Labels.ReviewsOpenIn(rows.Select(r => (r.Repo, r.Number)).Distinct().Count(), rows.Select(r => r.Repo).Distinct(StringComparer.Ordinal).Count()));
            }
        }

        SubLine = string.Join(" · ", parts);
    }
}
