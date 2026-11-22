using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The form's start and what follows it (<see cref="NewAgentFormViewModel"/>, continued): Start, Retry, the sign-in copy and "Show it".</summary>
#pragma warning disable MA0182 // why: the Agents page wires the form with its view (DG2)
internal sealed partial class NewAgentFormViewModel
{
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
