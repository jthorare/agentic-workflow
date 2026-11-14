using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The New agent form in the shell (<see cref="ShellViewModel"/>, continued; agents spec §15.4, U8): N on the Agents page opens it, a 2xx selects the new row and asks for its pane.</summary>
internal sealed partial class ShellViewModel
{
    private NewAgentFormViewModel? _newAgent;

    /// <summary>The form; one for the whole run, so what was typed survives Esc.</summary>
    public NewAgentFormViewModel NewAgent => _newAgent ??= Wired(new NewAgentFormViewModel(Health.ApiUrl, _api, Health.Catalog.Clipboard, [], new NewAgentDefaults(Repo: null, Agent: null), () => Guid.NewGuid().ToString("N")[..16]));

    /// <summary>Raised when a start succeeded and its pane should open (the terminal view is the Agents page's).</summary>
    internal event EventHandler<AgentOpenRequestedEventArgs>? AgentPaneRequested;

    // The Agents page's N, and a 2xx: the new row is selected (now or when the list draws it) and its pane is asked for.
    private void WireNewAgent()
    {
        Agents.NewAgentRequested += (_, _) => _ = OpenNewAgent(CancellationToken.None);
        _ = NewAgent;
    }

    private NewAgentFormViewModel Wired(NewAgentFormViewModel form)
    {
        form.OpenRequested += (_, e) =>
        {
            Agents.SelectWhenDrawn(e.Started.Id);
            AgentPaneRequested?.Invoke(this, e);
        };
        return form;
    }

    /// <summary>Reads the repos and last choices, then shows the form.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes once the form is shown.</returns>
    internal async Task OpenNewAgent(CancellationToken cancellationToken)
    {
        if (Health.ApiUrl() is { } url)
        {
            var setup = await _api.Setup(url, cancellationToken).ConfigureAwait(true);
            var last = await _api.LastStart(url, cancellationToken).ConfigureAwait(true);
            var repos = setup.IsSuccess
                ? setup.Value.Steps.Where(s => string.Equals(s.Id, "repo", StringComparison.Ordinal)).SelectMany(s => s.Items ?? []).Select(i => new RepoChoice(i.Repo, string.Equals(i.State, "done", StringComparison.Ordinal))).ToList()
                : [];
            NewAgent.SetRepos(repos, last.IsSuccess ? new NewAgentDefaults(last.Value.Repo, last.Value.Agent) : new NewAgentDefaults(Repo: null, Agent: null));
        }

        NewAgent.Open();
    }
}
