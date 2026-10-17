namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A terminal window that shows a launched session (launcher spec SL3; port; adapters: warp, exec-template, print). It never starts
/// or stops the agent on a tmux host, so its failure never stops a session (SL5).
/// </summary>
public interface ITerminalViewer
{
    /// <summary>The viewer kind ('warp', 'exec-template', 'print').</summary>
    public string Kind { get; }

    /// <summary>Opens a window that runs <see cref="ViewerRequest.AttachArgv"/>.</summary>
    /// <param name="request">What to show.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What happened; never throws for a viewer problem.</returns>
    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken);

    /// <summary>Whether this viewer can open a window here.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts the probe records; the id is <see cref="Kind"/>.</returns>
    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken);
}
