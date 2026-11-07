namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Whether the current caller may write to Linear at all: an owner needs a usable write key, a session never writes. The picked-teams check comes with the write routes (Linear hub 1b).</summary>
internal interface ILinearWriteGate
{
    /// <summary>Reads the gate for the current caller.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether writes are on, and the code when they are not.</returns>
    public ValueTask<LinearWriteGateView> Read(CancellationToken cancellationToken);
}
