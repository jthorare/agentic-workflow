using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Linear;

#pragma warning disable CS0067, CS0169, IDE0044, IDE0051, IDE0052, CA1822, CA1823, MA0041 // red stub: the read, the words and the PR chip arrive with the implementation

/// <summary>
/// The Linear issue's sheet, read only (Linear hub spec §3, "Issue sheet"): the key and title, a field grid, the description and the comments, newest first.
/// It reads GET /sindri/linear/issues/{key} on open and again on the events Work → Linear re-reads on. Text is plain: only http(s) links open, in the browser with
/// their host shown; images are never fetched and `sindri://` text is not a link.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="launcher">Opens a link in the system's browser.</param>
/// <param name="clock">The clock.</param>
internal sealed class LinearIssueSheetViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock) : ObservableObject
{
    private readonly ISindriApi _api = api;
    private readonly TimeProvider _clock = clock;
    private Uri? _url;
    private int _generation;

    /// <summary>The issue's PR chip asked for its review page.</summary>
    internal event EventHandler<LinearPrRequestedEventArgs>? PrRequested;

    /// <summary>The issue key; empty before the first.</summary>
    public string Key { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase { get; private set => Set(ref field, value); }

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an issue is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True while the sheet is a sentence instead of an issue.</summary>
    public bool IsProblem => Phase is FramePhase.NotFound or FramePhase.Refused or FramePhase.Unreadable;

    /// <summary>The first read pending.</summary>
    public string LoadingText => string.Empty;

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
    public string PrText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a PR is linked.</summary>
    public bool HasPr => PrText.Length > 0;

    /// <summary>True when the PR chip opens a review page; false when it is plain text.</summary>
    public bool CanOpenPr { get; private set => Set(ref field, value); }

    /// <summary>The description, a line each.</summary>
    public IReadOnlyList<LinearTextLine> Description { get; private set => Set(ref field, value); } = [];

    /// <summary>The comments, newest first.</summary>
    public IReadOnlyList<LinearCommentViewModel> Comments { get; private set => Set(ref field, value); } = [];

    /// <summary>The "Open in Linear" button's text.</summary>
    public string OpenInLinearText => string.Empty;

    /// <summary>True when the sheet offers Open in Linear (a key Sindri has not synced).</summary>
    public bool CanOpenInLinear => false;

    /// <summary>Opens a link of the sheet and keeps the system's answer.</summary>
    public LinkLaunch LinkLaunch { get; } = new LinkLaunch(launcher);

    /// <summary>Names the issue now, before any read.</summary>
    /// <param name="key">The issue key.</param>
    internal void Show(string key) => throw new NotSupportedException();

    /// <summary>Reads the issue behind a key and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="key">The issue key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string key, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Reads the open issue again.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The PR chip: asks for the PR's review page.</summary>
    internal void OpenPr() => throw new NotSupportedException();

    /// <summary>Opens the issue in Linear, in the browser.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the system has taken it.</returns>
    internal Task OpenInLinear(CancellationToken cancellationToken) => throw new NotSupportedException();
}
