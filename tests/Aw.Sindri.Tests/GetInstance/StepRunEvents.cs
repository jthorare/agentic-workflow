using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>An instance's workflow events, fixed; remembers which instance it was asked for.</summary>
internal sealed class StepRunEvents(params RecordedWorkflowEvent[] events) : IStepRunSource
{
    private readonly List<(InstanceAddress Address, RecordedWorkflowEvent Event)> _rows = [];
    private Error? _failure;

    /// <summary>The instance the last read asked for.</summary>
    public InstanceAddress? Asked { get; private set; }

    /// <summary>A source whose ledger can't be read.</summary>
    public static StepRunEvents Failing() => new() { _failure = SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full") };

    /// <summary>A source holding events of several instances, each tagged with its own; a read keeps the asked instance's.</summary>
    /// <param name="rows">The events, in ledger order.</param>
    public static StepRunEvents Of(params (InstanceAddress Address, RecordedWorkflowEvent Event)[] rows)
    {
        var source = new StepRunEvents();
        source._rows.AddRange(rows);
        return source;
    }

    // Two modes: built with events, every read gets them whatever the address; built with Of(rows), a read keeps the rows tagged with the asked address (the contract's mode).
    public ValueTask<Result<IReadOnlyList<RecordedWorkflowEvent>>> EventsOf(InstanceAddress address, CancellationToken cancellationToken)
    {
        Asked = address;
        if (_failure is { } failure)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<RecordedWorkflowEvent>>(failure));
        }

        IReadOnlyList<RecordedWorkflowEvent> kept = _rows.Count == 0 ? events : [.. _rows.Where(r => r.Address == address).Select(static r => r.Event)];
        return ValueTask.FromResult(Results.Success(kept));
    }
}
