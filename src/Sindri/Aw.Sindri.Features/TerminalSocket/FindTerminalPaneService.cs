using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>Finds a session's tmux pane through the locator: SND-AGENT-015 when the session has no pane, SND-AGENT-016 when it is gone.</summary>
internal sealed class FindTerminalPaneService(ITerminalPaneLocator locator) : IService<FindTerminalPaneRequest, FindTerminalPaneResponse>
{
    /// <inheritdoc/>
    public async ValueTask<Result<FindTerminalPaneResponse>> Handle(FindTerminalPaneRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pane = await locator.Find(request.Session, cancellationToken).ConfigureAwait(false);
        return pane.IsError ? pane.Error : new FindTerminalPaneResponse(pane.Value);
    }
}
