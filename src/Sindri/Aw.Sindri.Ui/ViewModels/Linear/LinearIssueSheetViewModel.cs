using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The Linear issue's sheet, read only (Linear hub spec §3, "Issue sheet"): the key and title, a field grid, the description and the comments, newest first.
/// It reads GET /sindri/linear/issues/{key} on open and again on the events Work → Linear re-reads on. Text is plain: only http(s) links open, in the browser with
/// their host shown; images are never fetched and `sindri://` text is not a link.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="launcher">Opens a link in the system's browser.</param>
/// <param name="clock">The clock.</param>
internal sealed partial class LinearIssueSheetViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock) : ObservableObject
{
    private readonly TimeProvider _clock = clock;
    private Uri? _url;
    private int _generation;
    private (string Repo, int Number)? _pr;

    /// <summary>The issue's PR chip asked for its review page.</summary>
    internal event EventHandler<LinearPrRequestedEventArgs>? PrRequested;

    /// <summary>The issue key; empty before the first.</summary>
    public string Key { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsShown));
            OnChanged(nameof(IsProblem));
        }
    }

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an issue is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True while the sheet is a sentence instead of an issue.</summary>
    public bool IsProblem => Phase is FramePhase.NotFound or FramePhase.Refused or FramePhase.Unreadable;

    /// <summary>The first read pending.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string LoadingText => Labels.LinearIssueLoading;
#pragma warning restore CA1822, MA0041

    /// <summary>The problem's sentence.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Under the fields: the last refresh failed and the issue is the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"No longer served" note when Linear no longer serves the issue; empty otherwise.</summary>
    public string RemovedNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The title; empty until read.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The field grid, in reading order.</summary>
    public IReadOnlyList<LinearIssueFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>"PR #2071 waiting on you"; empty without a PR.</summary>
    public string PrText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasPr));
        }
    } = string.Empty;

    /// <summary>True when a PR is linked.</summary>
    public bool HasPr => PrText.Length > 0;

    /// <summary>The PR chip's accessible name.</summary>
    public string PrName => CanOpenPr ? Labels.LinearPrChipOpens(PrText) : PrText;

    /// <summary>True when the PR chip opens a review page; false when it is plain text.</summary>
    public bool CanOpenPr
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(PrName));
        }
    }

    /// <summary>The description, a line each.</summary>
    public IReadOnlyList<LinearTextLine> Description
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasDescription));
        }
    } = [];

    /// <summary>True when the issue has a description.</summary>
    public bool HasDescription => Description.Count > 0;

    /// <summary>The comments, newest first.</summary>
    public IReadOnlyList<LinearCommentViewModel> Comments
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CommentsHeading));
            OnChanged(nameof(HasComments));
        }
    } = [];

    /// <summary>The "Open in Linear" button's text.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string OpenInLinearText => Labels.LinearOpenIn;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the sheet offers Open in Linear (a key Sindri has not synced).</summary>
    public bool CanOpenInLinear => Phase == FramePhase.NotFound;

    /// <summary>The heading over the description.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DescriptionHeading => Labels.LinearIssueDescription;

    /// <summary>The line when the issue has no description.</summary>
    public string NoDescriptionText => Labels.LinearIssueNoDescription;
#pragma warning restore CA1822, MA0041

    /// <summary>The heading over the comments.</summary>
    public string CommentsHeading => Labels.LinearIssueComments(Comments.Count);

    /// <summary>True when there are comments.</summary>
    public bool HasComments => Comments.Count > 0;

    /// <summary>The mark on a comment Sindri wrote.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string FromSindriText => Labels.LinearFromSindri;
#pragma warning restore CA1822, MA0041

    private ISindriApi Api { get; } = api;

    /// <summary>Opens a link of the sheet and keeps the system's answer.</summary>
    public LinkLaunch LinkLaunch { get; } = new LinkLaunch(launcher);

    /// <summary>Names the issue now, before any read.</summary>
    /// <param name="key">The issue key.</param>
    internal void Show(string key)
    {
        if (string.Equals(key, Key, StringComparison.Ordinal))
        {
            return;
        }

        _generation++;
        Clear();
        Key = key;
        Phase = FramePhase.Loading;
    }

    /// <summary>Reads the issue behind a key and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="key">The issue key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string key, CancellationToken cancellationToken)
    {
        Show(key);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the open issue again (every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Key.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    /// <summary>The PR chip: asks for the PR's review page.</summary>
    internal void OpenPr()
    {
        if (_pr is var (repo, number))
        {
            PrRequested?.Invoke(this, new LinearPrRequestedEventArgs(repo, number));
        }
    }

    /// <summary>Opens the issue in Linear, in the browser: Linear's own address for the key, which sends the owner to the issue's workspace.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the system has taken it.</returns>
    internal async Task OpenInLinear(CancellationToken cancellationToken)
    {
        if (CanOpenInLinear)
        {
            _ = await LinkLaunch.Open(new Uri("https://linear.app/issue/" + Uri.EscapeDataString(Key)), cancellationToken).ConfigureAwait(true);
        }
    }
}
