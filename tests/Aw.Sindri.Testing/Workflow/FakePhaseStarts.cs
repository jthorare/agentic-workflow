using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IPhaseStarts"/> that records what it was asked to start: a phase and attempt asked for again reports <see cref="PhaseStarted.Existed"/>, as the real address-keyed start does.</summary>
public sealed class FakePhaseStarts : IPhaseStarts
{
    private readonly List<PhaseStart> _started = [];

    /// <summary>Every distinct start, in order.</summary>
    public IReadOnlyList<PhaseStart> Started => _started;

    /// <summary>When set, every start fails with it.</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<PhaseStarted>> Start(PhaseStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is { } error)
        {
            return ValueTask.FromResult<Result<PhaseStarted>>(error);
        }

        var address = start.Line.Linked(start.Recipe, start.Phase, start.Attempt);
        var existed = _started.Exists(s => s.Line.Linked(s.Recipe, s.Phase, s.Attempt) == address);
        if (!existed)
        {
            _started.Add(start);
        }

        return ValueTask.FromResult<Result<PhaseStarted>>(new PhaseStarted(address, existed));
    }
}
