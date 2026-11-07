using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearWriteGate"/>: writes on, or off with a chosen code.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearWriteGate is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearWriteGate : ILinearWriteGate
{
    private LinearWriteGateView _gate = new(Allowed: true, Code: null);

    /// <summary>Answers every read with writes off and <paramref name="code"/>.</summary>
    /// <param name="code">Why not.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearWriteGate Off(string code)
    {
        _gate = new LinearWriteGateView(Allowed: false, code);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<LinearWriteGateView> Read(CancellationToken cancellationToken) => ValueTask.FromResult(_gate);
}
