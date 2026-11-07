namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The write gate over Context (red: not built yet).</summary>
internal sealed class ContextLinearWriteGate : ILinearWriteGate
{
    public ValueTask<LinearWriteGateView> Read(CancellationToken cancellationToken) => throw new NotSupportedException();
}
