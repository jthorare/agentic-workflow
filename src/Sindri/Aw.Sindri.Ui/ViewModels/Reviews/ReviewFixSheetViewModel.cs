using System.Globalization;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// The fix-task sheet (review page spec §8.2, §6.3, §6.4, §16): a text box pre-filled for a check or a requirement, exactly what Send will do, the Sending pattern,
/// the outcomes (babysitter, session, waiting, item only), the half-done send with Retry, and where focus goes after.
/// </summary>
internal sealed partial class ReviewFixSheetViewModel : ObservableObject
{
    private const int Limit = 2000;
    private static readonly TimeSpan _sendingAfter = TimeSpan.FromMilliseconds(300);

    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private Uri? _url;
    private ReviewFixRequestedEventArgs? _request;
    private string? _item;
    private int _opened;   // counts opens and closes: an answer for an earlier sheet is dropped

    internal ReviewFixSheetViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
    }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"From".</summary>
    public string FromText => Labels.ReviewFixFrom;

    /// <summary>"What needs fixing".</summary>
    public string WhatNeedsText => Labels.ReviewFixWhatNeeds;

    /// <summary>"What Send does".</summary>
    public string WhatSendDoesText => Labels.ReviewFixWhatSendDoes;

    /// <summary>The longest text the box takes (2,000 characters), the same limit <see cref="CanSend"/> checks.</summary>
    public int TextLimit => Limit;
#pragma warning restore CA1822, MA0041

    /// <summary>"Send as fix task"; "Fix task sent" or "Fix task not fully sent" after a send.</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"acme/api #2071 · feat/retry-budget · a1b2c3d".</summary>
    public string SubLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The From box's first line.</summary>
    public string SourceTitle { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The From box's second line.</summary>
    public string SourceDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What needs fixing (up to 2,000 characters): pre-filled for a check or a requirement, empty for a line.</summary>
    public string Text
    {
        get;
        set
        {
            if (IsSending)
            {
                return;
            }

            var changed = !string.Equals(field, value, StringComparison.Ordinal);
            Set(ref field, value);
            if (changed)
            {
                Refresh();
            }
        }
    } = string.Empty;

    /// <summary>"132 / 2,000".</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The item's title: "Fix: " and the first line cut at 44 characters with an ellipsis.</summary>
    public string ItemTitle { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The PR thread line of "What Send does".</summary>
    public string ThreadText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"The babysitter watching #2071, else a live session on feat/retry-budget, else the item waits in Work".</summary>
    public string GoesToText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when Send can be pressed: some text, at most 2,000 characters, nothing sent or sending.</summary>
    public bool CanSend { get; private set => Set(ref field, value); }

    /// <summary>True from the press until the answer; the text is locked and a second press does nothing.</summary>
    public bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>"Send", or "Sending…" once 300 ms have passed.</summary>
    public string SendText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What the send did.</summary>
    public ReviewFixOutcome Outcome { get; private set => Set(ref field, value); }

    /// <summary>The outcome's words, in a focusable text.</summary>
    public string OutcomeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The item made in Work; empty before a send.</summary>
    public string ItemId { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"#2071 · review comment", "#2071 · X.cs:11" or "not posted"; empty before a send.</summary>
    public string ThreadResult { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>SND-REVIEW-006 on a half-done send; empty otherwise.</summary>
    public string Code { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True after a half-done send: Retry post reuses the item and the same marker.</summary>
    public bool CanRetry { get; private set => Set(ref field, value); }

    /// <summary>True while the sheet is open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>True when the sheet shows its outcome instead of the form.</summary>
    public bool HasOutcome => Outcome is not (ReviewFixOutcome.None or ReviewFixOutcome.Failed);

    /// <summary>Where focus goes next: the text box on open, the outcome text after a send.</summary>
    public ReviewFocus Focus
    {
        get;
        private set
        {
            field = value;
            OnChanged(nameof(Focus));
        }
    } = ReviewFocus.None;

    /// <summary>The sheet closed: focus goes back to the diff line or panel row it came from.</summary>
    internal event EventHandler<ReviewFocusEventArgs>? Closed;

    /// <summary>Opens the sheet on a request from the page.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="request">The finding, the PR and where focus returns.</param>
    internal void Open(Uri url, ReviewFixRequestedEventArgs request)
    {
        (_url, _request, _item) = (url, request, null);
        _opened++;
        var (source, target) = (request.Source, request.Target);
        Heading = Labels.ReviewFixHeading;
        SubLine = Labels.ReviewFixSubLine(target.Repo, target.Number, target.Branch, target.Sha7);
        (SourceTitle, SourceDetail) = source.Kind switch
        {
            ReviewFixKind.Check => (Labels.ReviewFixCheckTitle(Labels.ReviewCheckFail, source.Title), Labels.ReviewFixCheckDetail(source.Detail)),
            ReviewFixKind.Requirement => ($"{source.SourceId}  {source.Title}", Labels.ReviewFixRequirementDetail(target.DocId)),
            ReviewFixKind.Line => (LineName(source), source.Added ? Labels.ReviewFixAddedLine : source.Removed ? Labels.ReviewFixRemovedLine : Labels.ReviewFixLine),
            _ => throw new ArgumentOutOfRangeException(nameof(request), source.Kind, message: null),
        };
        ThreadText = ThreadOf(source, target);
        GoesToText = Labels.ReviewFixGoesTo(target.Number, target.Branch);
        (Outcome, OutcomeText, ItemId, ThreadResult, Code, CanRetry, IsSending, SendText, IsOpen) = (ReviewFixOutcome.None, string.Empty, string.Empty, string.Empty, string.Empty, false, false, Labels.ReviewFixSend, true);
        OnChanged(nameof(HasOutcome));
        Text = source.Kind switch
        {
            ReviewFixKind.Check => Labels.ReviewFixCheckText(source.Title, source.Detail),
            ReviewFixKind.Requirement => Labels.ReviewFixRequirementText(source.SourceId, source.Title, target.DocId),
            ReviewFixKind.Line => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(request), source.Kind, message: null),
        };
        Refresh();
        Focus = new ReviewFocus(ReviewFocusRegion.FixText);
    }

    /// <summary>Send: makes the item and, on the owner's or an agent's PR, posts the thread.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the outcome is shown.</returns>
    internal Task Send(CancellationToken cancellationToken) => CanSend ? Run(cancellationToken) : Task.CompletedTask;

    /// <summary>Retry post after a half-done send: the same item and marker.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the outcome is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => CanRetry && !IsSending ? Run(cancellationToken) : Task.CompletedTask;

    /// <summary>Esc, Cancel or Close: closes the sheet and returns focus to where the request came from.</summary>
    internal void Close()
    {
        _opened++;
        (IsOpen, IsSending) = (false, false);
        Closed?.Invoke(this, new ReviewFocusEventArgs(_request?.ReturnTo ?? ReviewFocus.None));
    }

    private static string LineName(ReviewFixSource source) => string.Create(CultureInfo.InvariantCulture, $"{source.Path}:{source.Line}");

    private void Refresh()
    {
        var first = Text.Split('\n')[0];
        (CountText, ItemTitle) = (Labels.ReviewFixCount(Text.Length), Labels.ReviewFixItemTitle(first));
        CanSend = !IsSending && (Outcome is ReviewFixOutcome.None or ReviewFixOutcome.Failed) && !string.IsNullOrWhiteSpace(Text) && Text.Length <= Limit;
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        if (_url is not { } url || _request is not { } request)
        {
            return;
        }

        var (source, target) = (request.Source, request.Target);
        var opened = _opened;
        IsSending = true;
        CanSend = false;
        using var done = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ShowSending(done.Token);
        var send = SendOf(source, target);
        var answer = await _api.SendReviewFix(url, target.Repo, target.Number, send, cancellationToken).ConfigureAwait(true);
        await done.CancelAsync().ConfigureAwait(true);
        if (opened != _opened)
        {
            return;   // the sheet was closed or opened again while the send ran: that answer is not this sheet's
        }

        IsSending = false;
        SendText = Labels.ReviewFixSend;
        if (answer.IsError)
        {
            (Outcome, OutcomeText) = (ReviewFixOutcome.Failed, answer.Error.Message);
            ItemId = string.Empty;
            Refresh();
            OnChanged(nameof(HasOutcome));
            return;
        }

        Show(answer.Value, request);
    }

    private async Task ShowSending(CancellationToken token)
    {
        try
        {
            await Task.Delay(_sendingAfter, _clock, token).ConfigureAwait(true);
            if (IsSending)
            {
                SendText = Labels.ReviewFixSending;
            }
        }
        catch (OperationCanceledException)
        {
            // the answer came first
        }
    }

    // a removed line goes on the old side with its old-file number; the server decides how an old-side comment posts
    private ReviewFixSend SendOf(ReviewFixSource source, ReviewFixTarget target) => source.Kind switch
    {
        ReviewFixKind.Line => new ReviewFixSend(KindWord(source.Kind), Text, target.HeadSha, SourceId: null, source.Path, source.Line, source.Removed ? "old" : "new", _item),
        ReviewFixKind.Check or ReviewFixKind.Requirement => new ReviewFixSend(KindWord(source.Kind), Text, target.HeadSha, source.SourceId, Path: null, Line: null, Side: null, _item),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, message: null),
    };

    private static string ThreadOf(ReviewFixSource source, ReviewFixTarget target) => !target.PostsThreads
        ? Labels.ReviewFixItemOnly(target.Author)
        : source.Kind switch
        {
            ReviewFixKind.Line when source.Removed => Labels.ReviewFixRemovedThread(target.Number, source.Path, source.Line ?? 0),
            ReviewFixKind.Line => Labels.ReviewFixLineThread(source.Path, source.Line ?? 0),
            ReviewFixKind.Check or ReviewFixKind.Requirement => Labels.ReviewFixBodyThread(target.Number),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, message: null),
        };

    private static string KindWord(ReviewFixKind kind) => kind switch
    {
        ReviewFixKind.Check => "check",
        ReviewFixKind.Requirement => "requirement",
        ReviewFixKind.Line => "line",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
    };
}
