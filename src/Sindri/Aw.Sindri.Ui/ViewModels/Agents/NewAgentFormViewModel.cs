using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The New agent form (agents spec §15.4, U8): a sheet that starts a one-off agent. Ctrl+Enter starts and closes it on a 2xx; Ctrl+Shift+Enter
/// starts and keeps it open for the next one; Esc closes it and keeps what was typed; an error shows in the form and keeps the prompt and the actId.
/// </summary>
#pragma warning disable MA0182 // why: the Agents page wires the form with its view (DG2)
internal sealed partial class NewAgentFormViewModel : ObservableObject
{
    private static readonly string[] _derived =
    [
        nameof(IsStarting), nameof(CanStart), nameof(StartText), nameof(StatusText), nameof(RepoNote), nameof(RepoError), nameof(AgentError),
        nameof(SignInCommand), nameof(PromptError), nameof(PromptCount), nameof(HasSignInCommand), nameof(HasRepoNote), nameof(HasRepoError), nameof(HasAgentError), nameof(HasPromptError), nameof(HasFootError), nameof(HasFootLine), nameof(FootError), nameof(CanRetry), nameof(FootLine),
    ];

    private readonly Func<Uri?> _url;
    private readonly INewAgentRoute _route;
    private readonly IClipboard _clipboard;
    private readonly Func<string> _newActId;
    private bool _lastBackground;
    private bool _serverPromptRefusal;
    private AgentStartedViewRef? _lastStarted;

    internal NewAgentFormViewModel(Func<Uri?> url, INewAgentRoute route, IClipboard clipboard, IReadOnlyList<RepoChoice> repos, NewAgentDefaults defaults, Func<string> newActId)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(defaults);
        (_url, _route, _clipboard, _newActId) = (url, route, clipboard, newActId);
        Repos = repos;
        Repo = repos.FirstOrDefault(r => string.Equals(r.Name, defaults.Repo, StringComparison.Ordinal)) ?? (repos.Count > 0 ? repos[0] : null);
        Agent = AgentChoice.All.FirstOrDefault(a => string.Equals(a.Id, defaults.Agent, StringComparison.Ordinal)) ?? AgentChoice.ClaudeCode;
        ActId = newActId();
    }

    /// <summary>The fields in Tab order.</summary>
    internal static IReadOnlyList<NewAgentField> FocusOrder { get; } = [NewAgentField.Prompt, NewAgentField.Repo, NewAgentField.Agent, NewAgentField.Worktree, NewAgentField.Start];

    /// <summary>Raised when the page should open a session's terminal view.</summary>
    internal event EventHandler<AgentOpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when focus should move to a field.</summary>
    internal event EventHandler<NewAgentFocusEventArgs>? FocusRequested;

    /// <summary>True while the sheet is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The field with focus.</summary>
    public NewAgentField Focused { get; private set => Set(ref field, value); }

    /// <summary>The prompt as typed. Typing clears a refusal under it.</summary>
    public string Prompt
    {
        get;
        set
        {
            Set(ref field, value);
            _serverPromptRefusal = false;
            Changed();
        }
    } = string.Empty;

    /// <summary>The repos to choose from.</summary>
    public IReadOnlyList<RepoChoice> Repos { get; private set => Set(ref field, value); } = [];

    /// <summary>Takes the profile's repos and the last choices once they are read; what was typed or chosen already stays.</summary>
    /// <param name="repos">The repos.</param>
    /// <param name="defaults">The last start's repo and agent.</param>
    internal void SetRepos(IReadOnlyList<RepoChoice> repos, NewAgentDefaults defaults) => _ = (repos, defaults);

    /// <summary>The chosen repo; null when the profile has none. Choosing clears the error under it.</summary>
    public RepoChoice? Repo
    {
        get;
        set
        {
            Set(ref field, value);
            RepoError = string.Empty;
            Changed();
        }
    }

    /// <summary>The chosen agent. Choosing clears the error under it.</summary>
    public AgentChoice Agent
    {
        get;
        set
        {
            Set(ref field, value);
            (AgentError, SignInCommand) = (string.Empty, string.Empty);
            Changed();
        }
    }

    /// <summary>True to make a one-off worktree first.</summary>
    public bool NewWorktree { get; set => Set(ref field, value); }

    /// <summary>True while a start is on its way; the fields are locked.</summary>
    public bool IsStarting { get; private set; }

    /// <summary>True when Start can be pressed.</summary>
    public bool CanStart => IsOpen && !IsStarting && Repo is not null && Prompt.Length <= Labels.NewAgentPromptLimit;

    /// <summary>The Start button's words.</summary>
    public string StartText => IsStarting ? Labels.NewAgentStarting : Labels.NewAgentStart;

    /// <summary>"Making the worktree" while a start with the switch on runs; else empty.</summary>
    public string StatusText => IsStarting && NewWorktree ? Labels.NewAgentMakingWorktree : string.Empty;

    /// <summary>"not onboarded" when the chosen repo is not; else empty.</summary>
    public string RepoNote => Repo is { Onboarded: false } ? Labels.NewAgentNotOnboarded : string.Empty;

    /// <summary>The prompt's counter: "37 / 8,000".</summary>
    public string PromptCount => Labels.NewAgentCount(Prompt.Length);

    /// <summary>True when the Agent error has a sign-in command to copy.</summary>
    public bool HasSignInCommand => SignInCommand.Length > 0;

    /// <summary>True when the chosen repo is not onboarded.</summary>
    public bool HasRepoNote => RepoNote.Length > 0;

    /// <summary>True when an error shows under Repo.</summary>
    public bool HasRepoError => RepoError.Length > 0;

    /// <summary>True when an error shows under Agent.</summary>
    public bool HasAgentError => AgentError.Length > 0;

    /// <summary>True when an error shows under Prompt.</summary>
    public bool HasPromptError => PromptError.Length > 0;

    /// <summary>True when an error shows at the foot.</summary>
    public bool HasFootError => FootError.Length > 0;

    /// <summary>True when the background-start line shows.</summary>
    public bool HasFootLine => FootLine.Length > 0;

    /// <summary>The error under Repo.</summary>
    public string RepoError { get; private set; } = string.Empty;

    /// <summary>The error under Agent.</summary>
    public string AgentError { get; private set; } = string.Empty;

    /// <summary>The sign-in command the Agent error carries; empty for none (no Copy command button).</summary>
    public string SignInCommand { get; private set; } = string.Empty;

    /// <summary>The error under Prompt: live over the limit, or after the server refused the length.</summary>
    public string PromptError => Prompt.Length > Labels.NewAgentPromptLimit || _serverPromptRefusal ? Labels.NewAgentPromptTooLong(Prompt.Length) : string.Empty;

    /// <summary>The error at the foot of the form.</summary>
    public string FootError { get; private set; } = string.Empty;

    /// <summary>True when the foot error has a Retry button.</summary>
    public bool CanRetry => FootError.Length > 0;

    /// <summary>The line after a background start: "Started claude in sindri."; empty for none.</summary>
    public string FootLine { get; private set; } = string.Empty;


    /// <summary>The idempotency key the next start sends; kept until a 2xx.</summary>
    internal string ActId { get; private set; }

    /// <summary>Shows the sheet with focus on Prompt, keeping what was typed.</summary>
    internal void Open()
    {
        IsOpen = true;
        (RepoError, AgentError, SignInCommand, FootError) = (string.Empty, string.Empty, string.Empty, string.Empty);
        Focus(NewAgentField.Prompt);
        Changed();
    }

    /// <summary>Closes the sheet and keeps what was typed.</summary>
    internal void Close()
    {
        IsOpen = false;
        Changed();
    }

    /// <summary>Moves focus to the next field (or the previous), wrapping.</summary>
    /// <param name="back">True for Shift+Tab.</param>
    internal void MoveFocus(bool back)
    {
        var at = FocusOrder.ToList().IndexOf(Focused);
        var next = (at + (back ? FocusOrder.Count - 1 : 1)) % FocusOrder.Count;
        Focus(FocusOrder[next]);
    }

    /// <summary>One key on the form; works from any field.</summary>
    /// <param name="key">Enter, Escape or Tab.</param>
    /// <param name="ctrl">Ctrl held.</param>
    /// <param name="shift">Shift held.</param>
    /// <param name="cancellationToken">Cancels a start.</param>
    /// <returns>True when the key was used.</returns>
    internal async Task<bool> Press(string key, bool ctrl, bool shift, CancellationToken cancellationToken)
    {
        if (!IsOpen)
        {
            return false;
        }

        switch (key)
        {
            case "Escape":
                Close();
                return true;
            case "Tab":
                MoveFocus(back: shift);
                return true;
            case "Enter" when ctrl:
                await Start(background: shift, cancellationToken).ConfigureAwait(true);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Sends the start.</summary>
    /// <param name="background">True to keep the form open with the prompt cleared.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Start(bool background, CancellationToken cancellationToken)
    {
        if (!CanStart || Repo is not { } repo || _url() is not { } url)
        {
            return;
        }

        _lastBackground = background;
        (RepoError, AgentError, SignInCommand, FootError, FootLine) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        _serverPromptRefusal = false;
        var request = new StartAgentRequest(repo.Name, Agent.Id, string.IsNullOrWhiteSpace(Prompt) ? null : Prompt, NewWorktree, ActId);
        IsStarting = true;
        Changed();
        Result<AgentStartedView> answer;
        try
        {
            answer = await _route.Start(url, request, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsStarting = false;
        }

        if (answer.IsSuccess)
        {
            Succeed(answer.Value, repo, background);
        }
        else
        {
            Refuse(answer.Error);
        }

        Changed();
    }

    /// <summary>Sends the same start again after the foot error.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => Start(_lastBackground, cancellationToken);

    /// <summary>Puts the sign-in command on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True once copied.</returns>
    internal async Task<bool> CopySignInCommand(CancellationToken cancellationToken) =>
        SignInCommand.Length > 0 && await _clipboard.SetText(SignInCommand, cancellationToken).ConfigureAwait(true);

    /// <summary>"Show it": opens the pane of the session the foot line names.</summary>
    internal void ShowIt()
    {
        if (_lastStarted is { } started)
        {
            OpenRequested?.Invoke(this, new AgentOpenRequestedEventArgs(started));
        }
    }
}
