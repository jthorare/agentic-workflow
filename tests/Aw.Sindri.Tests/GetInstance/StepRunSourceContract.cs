using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The step-run source contract (IStepRunSource, internal to Features; bindings hand over its read as a <see cref="StepRunRead"/>): an instance's own workflow events in ledger order, nothing of other instances, empty for an instance with no rows, and a read error comes back as an error.</summary>
public abstract class StepRunSourceContract
{
    /// <summary>The instance the contract asks about.</summary>
    protected static InstanceAddress Mine { get; } = InstanceAddress.From("T-1/step/1");

    /// <summary>Another instance of the same item.</summary>
    protected static InstanceAddress Other { get; } = InstanceAddress.From("T-1/step/2");

    /// <summary>The child of <see cref="Mine"/>, whose address starts with it.</summary>
    protected static InstanceAddress ChildOfMine { get; } = InstanceAddress.From("T-1/step/1/step.2");

    /// <summary>A source holding <paramref name="rows"/> in the given (ledger) order.</summary>
    /// <param name="rows">Each event with the instance that mirrored it.</param>
    /// <returns>The source under test.</returns>
    protected abstract ValueTask<StepRunRead> Create(IReadOnlyList<(InstanceAddress Address, RecordedWorkflowEvent Event)> rows);

    /// <summary>A source whose ledger can't be read.</summary>
    /// <returns>The source under test.</returns>
    protected abstract ValueTask<StepRunRead> CreateFailing();

    [Fact]
    public async Task Keeps_only_the_asked_instances_events_in_ledger_order()
    {
        var source = await Create(
        [
            (Mine, StepRunFixtures.Started("produce", "1.1")),
            (Other, StepRunFixtures.Started("other", "1.1", minutes: 1)),
            (ChildOfMine, StepRunFixtures.Started("child", "1.1", minutes: 2)),
            (Mine, StepRunFixtures.Completed("produce", "checks", "1.1", "done", minutes: 3)),
        ]);

        var read = await source(Mine, TestContext.Current.CancellationToken);

        Assert.Equal("produce checks", string.Join(' ', read.Value.Select(static e => e.Payload.To)));
    }

    [Fact]
    public async Task An_instance_with_no_rows_reads_as_an_empty_list()
    {
        var source = await Create([(Other, StepRunFixtures.Started("produce", "1.1"))]);

        var read = await source(Mine, TestContext.Current.CancellationToken);

        Assert.Empty(read.Value);
    }

    [Fact]
    public async Task A_read_error_comes_back_as_an_error()
    {
        var source = await CreateFailing();

        var read = await source(Mine, TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
    }
}
