using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>What an answer becomes in the form (<see cref="NewAgentFormViewModel"/>, continued): the 2xx, each refusal under its field, focus and change notices.</summary>
#pragma warning disable MA0182 // why: the Agents page wires the form with its view (DG2)
internal sealed partial class NewAgentFormViewModel
{
    private void Succeed(AgentStartedView view, RepoChoice repo, bool background)
    {
        var started = new AgentStartedViewRef(view.Id, view.ActId);
        _lastStarted = started;
        Prompt = string.Empty;
        ActId = _newActId();
        if (background)
        {
            FootLine = Labels.NewAgentStarted(Agent.Short, repo.Name);
            Focus(NewAgentField.Prompt);   // the next Ctrl+Enter starts the next one from here
            return;
        }

        IsOpen = false;
        OpenRequested?.Invoke(this, new AgentOpenRequestedEventArgs(started));
    }

    private void Refuse(Error error)
    {
        var words = string.IsNullOrWhiteSpace(error.Message) ? null : error.Message;
        switch (error.Code)
        {
            case "SND-AGENT-012":
                RepoError = words ?? Labels.NewAgentRepoFallback;
                Focus(NewAgentField.Repo);
                break;
            case "SND-AGENT-013":
                AgentError = words ?? Labels.NewAgentAgentFallback;
                SignInCommand = CommandOf(error);
                Focus(NewAgentField.Agent);
                break;
            case "SND-AGENT-017":
                _serverPromptRefusal = true;
                Focus(NewAgentField.Prompt);
                break;
            default:
                FootError = words ?? Labels.NewAgentFootFallback;
                Focus(NewAgentField.Start);
                break;
        }
    }

    private static string CommandOf(Error error)
    {
        foreach (var (name, value) in error.Extensions ?? ErrorExtensions.Empty)
        {
            if (string.Equals(name, "command", StringComparison.Ordinal) && value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private void Focus(NewAgentField target)
    {
        Focused = target;
        FocusRequested?.Invoke(this, new NewAgentFocusEventArgs(target));
    }

    private void Changed()
    {
        foreach (var name in _derived)
        {
            OnChanged(name);
        }
    }
}
