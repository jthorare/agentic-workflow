using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearWriteGate"/>: writes on, or off with a chosen code.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearWriteGate is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearWriteGate : ILinearWriteGate
{
    private string? _code;

    /// <summary>Every team a refusal was asked for, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>Answers every read with writes off and <paramref name="code"/> (SND-AUTH-002, SND-LINEAR-002 or SND-LINEAR-003).</summary>
    /// <param name="code">Why not.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearWriteGate Off(string code)
    {
        _code = code;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Error?> Refusal(string teamKey, CancellationToken cancellationToken)
    {
        Asked.Add(teamKey);
        return ValueTask.FromResult(_code is { } code ? LinearWriteRefusals.Of(code, teamKey) : null);
    }
}
