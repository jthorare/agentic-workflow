using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>Finds the tmux pane of a session (agents spec §15.2): the 'sindri-&lt;actId&gt;' name joins the launch socket, a discovered session joins its fleet facts by session name plus pane id.</summary>
public interface ITerminalPaneLocator
{
    /// <summary>Finds the pane.</summary>
    /// <param name="provider">The provider of the session.</param>
    /// <param name="id">The provider session id, or the tmux session name 'sindri-&lt;actId&gt;'.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The pane, SND-AGENT-015 when the session has no tmux pane (the message carries the host kind), or SND-AGENT-016 when the session is gone.</returns>
    public ValueTask<Result<TerminalTarget>> Find(string provider, string id, CancellationToken cancellationToken);
}
