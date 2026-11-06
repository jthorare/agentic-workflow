using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// The fix-task sheet (review page spec §8.2, §6.3, §6.4, §16): a text box pre-filled for a check or a requirement, exactly what Send will do, the Sending pattern,
/// the outcomes (babysitter, session, waiting, item only), the half-done send with Retry, and where focus goes after.
/// </summary>
internal sealed class ReviewFixSheetViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;

    internal ReviewFixSheetViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
    }

    /// <summary>"Send as fix task"; "Fix task sent" or "Fix task not fully sent" after a send.</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"acme/api #2071 · feat/retry-budget · a1b2c3d".</summary>
    public string SubLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The From box's first line ("fail  Recipe change shows Ui tests ran", "src/Api/A/A.Core/X.cs:11").</summary>
    public string SourceTitle { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The From box's second line ("Required check · ui-tests: failure on a1b2c3d").</summary>
    public string SourceDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What needs fixing (up to 2,000 characters): pre-filled for a check or a requirement, empty for a line.</summary>
    public string Text { get; set => Set(ref field, value); } = string.Empty;

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

    /// <summary>Where focus goes next: the text box on open, the outcome text after a send.</summary>
    public ReviewFocus Focus { get; private set => Set(ref field, value); } = ReviewFocus.None;

    /// <summary>Opens the sheet on a request from the page.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="request">The finding, the PR and where focus returns.</param>
    internal void Open(Uri url, ReviewFixRequestedEventArgs request)
    {
        _ = (url, request, _api, _clock);
        OnChanged(nameof(Heading));
    }

    /// <summary>Send: makes the item and, on the owner's or an agent's PR, posts the thread.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the outcome is shown.</returns>
    internal Task Send(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        OnChanged(nameof(Outcome));
        return Task.CompletedTask;
    }

    /// <summary>Retry post after a half-done send: the same item and marker.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the outcome is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        OnChanged(nameof(Outcome));
        return Task.CompletedTask;
    }

    /// <summary>Esc, Cancel or Close: closes the sheet and returns focus to where the request came from.</summary>
    internal void Close() => OnChanged(nameof(Focus));

#pragma warning disable CS0067 // raised once the sheet closes
    /// <summary>The sheet closed: focus goes back to the diff line or panel row it came from.</summary>
    internal event EventHandler<ReviewFocusEventArgs>? Closed;
#pragma warning restore CS0067
}
