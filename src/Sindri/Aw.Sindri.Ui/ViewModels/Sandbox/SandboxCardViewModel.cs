using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>
/// Health's Sandbox row and what Settings' Sandbox section shows (sandbox spec §8): read from GET /sindri/sandbox/status, which never probes, so
/// it is followed with the other views (read on connect and after every event). Check again, when the row is blocked or never checked, is
/// <see cref="Check"/>. A refused connection is "unknown" and dimmed; an answer this build can't read is "details unavailable".
/// </summary>
internal sealed class SandboxCardViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;

    /// <summary>The card over the API, a clock, the endpoint, the command sender and the clipboard.</summary>
    /// <param name="api">The API.</param>
    /// <param name="clock">For the days a learn window has left and the age of a check.</param>
    /// <param name="apiUrl">The API's base URL while connected, else null.</param>
    /// <param name="commands">Sends the answer-resume of one instance.</param>
    /// <param name="clipboard">Where a blocker's command is copied.</param>
    internal SandboxCardViewModel(ISindriApi api, TimeProvider clock, Func<Uri?> apiUrl, Func<ApplyCommandBody, Task<Result<ApplyCommandAccepted>?>> commands, IClipboard clipboard)
    {
        (_api, _clock) = (api, clock);
        Check = new SandboxCheckViewModel(api, clock, apiUrl, commands, clipboard);
        RowText = Labels.SandboxRowNeverChecked;
        StateWord = StateWordOf(SandboxHealthState.NeverChecked, waiting: false);
    }

    /// <summary>The card's heading.</summary>
    public string Heading { get; } = Labels.SandboxHeading;

    /// <summary>Check again, its outcome and the resume list; shared by every place that shows them.</summary>
    public SandboxCheckViewModel Check { get; }

    /// <summary>What the row says.</summary>
    public SandboxHealthState State { get; private set; } = SandboxHealthState.NeverChecked;

    /// <summary>The row's text: "Sandbox: ready", "Sandbox: " and the first blocker's sentence, and so on.</summary>
    public string RowText { get; private set; }

    /// <summary>The state in a word at the card's right.</summary>
    public string StateWord { get; private set; }

    /// <summary>True while Sindri doesn't answer: the row is dimmed.</summary>
    public bool IsDimmed => State == SandboxHealthState.ServeDown;

    /// <summary>True while a learn window is open: the row is a warning.</summary>
    public bool IsWarning => State == SandboxHealthState.Learning;

    /// <summary>The mark before the state's word: never colour alone.</summary>
    public StateGlyph Glyph { get; private set; }

    /// <summary>Every blocker the last check found.</summary>
    public IReadOnlyList<SandboxBlockerRow> Blockers { get; private set; } = [];

    /// <summary>True when the row offers Check again: blocked, or never checked.</summary>
    public bool ShowsCheck => State is SandboxHealthState.Blocked or SandboxHealthState.NeverChecked;

    /// <summary>What was read last; null before a read and when it failed.</summary>
    internal SandboxStatusView? Status { get; private set; }

    /// <summary>The open learn windows, soonest end first, with the whole days each has left.</summary>
    internal IReadOnlyList<SandboxLearnWindow> Learning { get; private set; } = [];

    /// <summary>Reads the status and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = await _api.SandboxStatus(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            var unreadable = string.Equals(read.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
            Status = null;
            Learning = [];
            Blockers = [];
            Check.Seen(checkedAt: null);
            Show(unreadable ? SandboxHealthState.Unreadable : SandboxHealthState.ServeDown, unreadable ? Labels.SandboxDetailsUnavailable : Labels.SandboxRowUnknown, StateGlyph.None);
            return;
        }

        var status = read.Value;
        Status = status;
        Check.Seen(status.CheckedAt);
        var now = _clock.GetUtcNow();
        Learning = [.. status.Learn.OrderBy(l => l.EndsAt).Select(l => new SandboxLearnWindow(l.Repo, Math.Max(1, (int)Math.Ceiling((l.EndsAt - now).TotalDays))))];
        Blockers = [.. status.Blockers.Select(SandboxBlockerRow.Of)];
        ShowRead(status);
    }

    // A blocker beats a learn window, which beats "not checked yet"; otherwise the sandbox is ready.
    private void ShowRead(SandboxStatusView status)
    {
        if (status.CheckedAt is not null && (status.Ready == false || Blockers.Count > 0))
        {
            var sentence = Blockers.Count > 0 ? Blockers[0].Text : Labels.SandboxCheckFailed;
            Show(SandboxHealthState.Blocked, Labels.SandboxRowBlocked(sentence), Blockers.Count > 0 && Blockers[0].IsWait ? StateGlyph.Stale : StateGlyph.Down);
        }
        else if (Learning.Count > 0)
        {
            Show(SandboxHealthState.Learning, Labels.SandboxRowLearning(Learning[0].Repo, Learning[0].Days), StateGlyph.Profile);
        }
        else if (status.CheckedAt is null)
        {
            Show(SandboxHealthState.NeverChecked, Labels.SandboxRowNeverChecked, StateGlyph.None);
        }
        else
        {
            Show(SandboxHealthState.Ready, Labels.SandboxRowReady, StateGlyph.Ok);
        }
    }

    private void Show(SandboxHealthState state, string row, StateGlyph glyph)
    {
        State = state;
        RowText = row;
        Glyph = glyph;
        StateWord = StateWordOf(state, waiting: Blockers.Count > 0 && Blockers[0].IsWait);
        OnChanged(nameof(State));
        OnChanged(nameof(RowText));
        OnChanged(nameof(StateWord));
        OnChanged(nameof(Glyph));
        OnChanged(nameof(Blockers));
        OnChanged(nameof(IsDimmed));
        OnChanged(nameof(IsWarning));
        OnChanged(nameof(ShowsCheck));
    }

    /// <summary>The state in a word at the card's right.</summary>
    /// <param name="state">The state.</param>
    /// <param name="waiting">True when the first blocker is a wait or not yet supported (never red).</param>
    /// <returns>"ready", "blocked", "waiting", "learning", "unknown" or "not checked".</returns>
    private static string StateWordOf(SandboxHealthState state, bool waiting) => state switch
    {
        SandboxHealthState.Ready => Labels.SandboxWordReady,
        SandboxHealthState.Blocked => waiting ? Labels.SandboxWordWaiting : Labels.SandboxWordBlocked,
        SandboxHealthState.Learning => Labels.SandboxWordLearning,
        SandboxHealthState.Unreadable => Labels.SandboxWordUnreadable,
        SandboxHealthState.ServeDown => Labels.SandboxWordUnknown,
        SandboxHealthState.NeverChecked => Labels.SandboxWordNeverChecked,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
