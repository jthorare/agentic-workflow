using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The New agent form (agents spec §15.4, U8): a sheet that starts a one-off agent. Ctrl+Enter starts and closes it on a 2xx; Ctrl+Shift+Enter
/// starts and keeps it open for the next one; Esc closes it and keeps what was typed; an error shows in the form and keeps the prompt and the actId.
/// </summary>
#pragma warning disable CS0067, CA1822, MA0041, MA0182 // stub: events and members come with the behavior
internal sealed class NewAgentFormViewModel : ObservableObject
{
    internal NewAgentFormViewModel(Func<Uri?> url, INewAgentRoute route, IClipboard clipboard, IReadOnlyList<RepoChoice> repos, NewAgentDefaults defaults, Func<string> newActId)
    {
        _ = (url, route, clipboard, repos, defaults, newActId);
    }

    /// <summary>The fields in Tab order.</summary>
    internal static IReadOnlyList<NewAgentField> FocusOrder { get; } = [NewAgentField.Prompt, NewAgentField.Repo, NewAgentField.Agent, NewAgentField.Worktree, NewAgentField.Start];

    /// <summary>Raised when the page should open a session's terminal view.</summary>
    internal event EventHandler<AgentOpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when focus should move to a field.</summary>
    internal event EventHandler<NewAgentFocusEventArgs>? FocusRequested;

    /// <summary>True while the sheet is shown.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The field with focus.</summary>
    public NewAgentField Focused { get; private set; }

    /// <summary>The prompt as typed.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The repos to choose from.</summary>
    public IReadOnlyList<RepoChoice> Repos { get; } = [];

    /// <summary>The chosen repo; null when the profile has none.</summary>
    public RepoChoice? Repo { get; set; }

    /// <summary>The chosen agent.</summary>
    public AgentChoice Agent { get; set; } = AgentChoice.ClaudeCode;

    /// <summary>True to make a one-off worktree first.</summary>
    public bool NewWorktree { get; set; }

    /// <summary>True while a start is on its way; the fields are locked.</summary>
    public bool IsStarting => throw new NotSupportedException();

    /// <summary>True when Start can be pressed.</summary>
    public bool CanStart => throw new NotSupportedException();

    /// <summary>The Start button's words.</summary>
    public string StartText => throw new NotSupportedException();

    /// <summary>"Making the worktree" while a start with the switch on runs; else empty.</summary>
    public string StatusText => throw new NotSupportedException();

    /// <summary>"not onboarded" when the chosen repo is not; else empty.</summary>
    public string RepoNote => throw new NotSupportedException();

    /// <summary>The error under Repo.</summary>
    public string RepoError => throw new NotSupportedException();

    /// <summary>The error under Agent.</summary>
    public string AgentError => throw new NotSupportedException();

    /// <summary>The sign-in command the Agent error carries; empty for none (no Copy command button).</summary>
    public string SignInCommand => throw new NotSupportedException();

    /// <summary>The error under Prompt.</summary>
    public string PromptError => throw new NotSupportedException();

    /// <summary>The error at the foot of the form.</summary>
    public string FootError => throw new NotSupportedException();

    /// <summary>True when the foot error has a Retry button.</summary>
    public bool CanRetry => throw new NotSupportedException();

    /// <summary>The line after a background start: "Started claude in sindri."; empty for none.</summary>
    public string FootLine => throw new NotSupportedException();

    /// <summary>The idempotency key the next start sends; kept until a 2xx.</summary>
    internal string ActId => throw new NotSupportedException();

    /// <summary>Shows the sheet with focus on Prompt, keeping what was typed.</summary>
    internal void Open() => throw new NotSupportedException();

    /// <summary>Closes the sheet and keeps what was typed.</summary>
    internal void Close() => throw new NotSupportedException();

    /// <summary>Moves focus to the next field (or the previous), wrapping.</summary>
    /// <param name="back">True for Shift+Tab.</param>
    internal void MoveFocus(bool back) => throw new NotSupportedException();

    /// <summary>One key on the form; works from any field.</summary>
    /// <param name="key">Enter, Escape or Tab.</param>
    /// <param name="ctrl">Ctrl held.</param>
    /// <param name="shift">Shift held.</param>
    /// <param name="cancellationToken">Cancels a start.</param>
    /// <returns>True when the key was used.</returns>
    internal Task<bool> Press(string key, bool ctrl, bool shift, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the start.</summary>
    /// <param name="background">True to keep the form open with the prompt cleared.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Start(bool background, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the same start again after the foot error.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Puts the sign-in command on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True once copied.</returns>
    internal Task<bool> CopySignInCommand(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>"Show it": opens the pane of the session the foot line names.</summary>
    internal void ShowIt() => throw new NotSupportedException();
}
