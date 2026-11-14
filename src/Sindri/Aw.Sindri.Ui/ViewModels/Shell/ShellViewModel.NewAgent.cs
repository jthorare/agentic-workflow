using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The New agent form in the shell (<see cref="ShellViewModel"/>, continued; agents spec §15.4, U8): N on the Agents page opens it, a 2xx selects the new row and asks for its pane.</summary>
#pragma warning disable CS0067 // the red: the event is raised once the behavior lands
internal sealed partial class ShellViewModel
{
    private NewAgentFormViewModel? _newAgent;

    /// <summary>The form; one for the whole run, so what was typed survives Esc.</summary>
    public NewAgentFormViewModel NewAgent => _newAgent ??= new NewAgentFormViewModel(Health.ApiUrl, _api, Health.Catalog.Clipboard, [], new NewAgentDefaults(Repo: null, Agent: null), () => Guid.NewGuid().ToString("N")[..16]);

    /// <summary>Raised when a start succeeded and its pane should open (the terminal view is the Agents page's).</summary>
    internal event EventHandler<AgentOpenRequestedEventArgs>? AgentPaneRequested;

    /// <summary>Reads the repos and last choices, then shows the form.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes once the form is shown.</returns>
    internal Task OpenNewAgent(CancellationToken cancellationToken) => Task.CompletedTask;
}
