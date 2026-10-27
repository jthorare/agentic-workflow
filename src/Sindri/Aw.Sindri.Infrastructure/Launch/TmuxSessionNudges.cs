using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Types the idle sweep's one re-prompt into a Step session's tmux pane. A session another host runs (T3) is not typed into: the sweep then counts it nudged and ends it if it stays idle.</summary>
/// <param name="tmux">The tmux host the Step sessions run on.</param>
internal sealed class TmuxSessionNudges(TmuxSessionHost tmux) : ISessionNudges
{
    /// <inheritdoc/>
    public ValueTask<bool> Send(LaunchId launch, string text, CancellationToken cancellationToken) =>
        tmux.SendLine(new HostRef(HostKind.Tmux, LaunchSessionName.For(launch)), text, cancellationToken);
}
