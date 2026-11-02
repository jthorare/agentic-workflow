using Aw.Kernel.Outbox;
using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing.Tests;

/// <summary>
/// The outbox contract must fail an outbox that drifts. Each broken outbox wraps the correct fake and changes one
/// behavior, so a failure points at exactly that behavior.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class OutboxContractCatchesBrokenFakesTests
{
    [Fact]
    public async Task An_outbox_that_accepts_a_duplicate_id_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new AcceptsDuplicates(o)).Recording_an_id_twice_is_a_conflict);

        Assert.Contains("AW-KERNEL-003", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_reads_newest_first_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new NewestFirst(o)).ReadPending_returns_the_oldest_first_up_to_max);

        Assert.Contains("must return the two oldest", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_reads_a_failed_command_before_its_next_attempt_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new Bound((c, p) => new ReadsTooEarly(new InMemoryOutbox(c, p))).A_failed_command_waits_for_its_next_attempt);

        Assert.Contains("29 s after the first failure the command must not be pending", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_backs_off_but_never_dead_letters_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(
            new Bound((c, p) => new InMemoryOutbox(c, OutboxRetryPolicy.Create(1_000, p.BaseDelay, p.MaxDelay).Value)).A_command_is_dead_lettered_when_the_policy_gives_up);

        Assert.Contains("must be dead-lettered", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_drops_failures_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new DropsFailures(o)).A_failed_command_waits_for_its_next_attempt);

        Assert.Contains("29 s after the first failure", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_ignores_the_unit_of_work_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => o, ignoreUnit: true).A_command_enqueued_in_a_rolled_back_unit_of_work_is_gone);

        Assert.Contains("disposed without Commit is still pending", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_keeps_the_callers_offset_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new KeepsOffset(o)).Times_are_read_back_in_utc);

        Assert.Contains("offset zero", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_dispatches_twice_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new DispatchesTwice(o)).A_dispatched_command_is_no_longer_pending);

        Assert.Contains("AW-KERNEL-002", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_throws_for_max_zero_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(Bound.Wrapping(o => new ThrowsOnZero(o)).ReadPending_with_max_below_one_is_invalid);

        _ = Assert.IsType<ArgumentOutOfRangeException>(e.InnerException);
    }

    private sealed class Bound(Func<TimeProvider, OutboxRetryPolicy, IOutbox> create, bool ignoreUnit = false) : OutboxContract
    {
        private InMemoryOutbox? _store;
        private IOutbox? _outbox;

        // A factory, not a second constructor: xUnit1056 wants one public constructor on a class with tests.
        public static Bound Wrapping(Func<IOutbox, IOutbox> breaker, bool ignoreUnit = false) => new((c, p) => breaker(new InMemoryOutbox(c, p)), ignoreUnit);

        protected override ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken)
        {
            _outbox = create(clock, policy);
            _store = Unwrap(_outbox);
            return ValueTask.FromResult(_outbox);
        }

        protected override ValueTask<IUnitOfWorkFactory> CreateUnitOfWorkFactory(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IUnitOfWorkFactory>(new InMemoryUnitOfWorkFactory());

        protected override IOutbox OutboxIn(IUnitOfWork work, TimeProvider clock, OutboxRetryPolicy policy) =>
            ignoreUnit ? _outbox! : _store!.In((InMemoryUnitOfWork)work); // why: the contract calls CreateOutbox first

        private static InMemoryOutbox Unwrap(IOutbox outbox) => outbox switch
        {
            InMemoryOutbox store => store,
            Passthrough wrapper => Unwrap(wrapper.Wrapped),
            _ => throw new InvalidOperationException("not an InMemoryOutbox"),
        };
    }

    private class Passthrough(IOutbox inner) : IOutbox
    {
        public IOutbox Wrapped => Inner;

        protected IOutbox Inner { get; } = inner;

        public virtual ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken) => Inner.Enqueue(command, cancellationToken);

        public virtual ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) => Inner.ReadPending(max, cancellationToken);

        public virtual ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
            Inner.MarkDispatched(id, dispatchedAt, cancellationToken);

        public virtual ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) => Inner.MarkFailed(id, error, cancellationToken);
    }

    private sealed class AcceptsDuplicates(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken)
        {
            _ = await Inner.Enqueue(command, cancellationToken);
            return Results.Ok();
        }
    }

    private sealed class NewestFirst(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
        {
            var all = await Inner.ReadPending(int.MaxValue, cancellationToken);
            return Results.Success<IReadOnlyList<OutboxCommand>>([.. all.Value.OrderByDescending(c => c.EnqueuedAt).Take(max)]);
        }
    }

    private sealed class DropsFailures(IOutbox inner) : Passthrough(inner)
    {
        public override ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Ok());
    }

    private sealed class DispatchesTwice(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken)
        {
            _ = await Inner.MarkDispatched(id, dispatchedAt, cancellationToken);
            return Results.Ok();
        }
    }

    private sealed class ThrowsOnZero(IOutbox inner) : Passthrough(inner)
    {
        public override ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
            return Inner.ReadPending(max, cancellationToken);
        }
    }

    private sealed class ReadsTooEarly(InMemoryOutbox inner) : Passthrough(inner)
    {
        public override ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlyList<OutboxCommand>>(
                [.. inner.Entries.Where(e => e.DispatchedAt is null && e.DeadLetteredAt is null).OrderBy(e => e.Command.EnqueuedAt).Take(max).Select(e => e.Command)]));
    }

    private sealed class KeepsOffset(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
        {
            var read = await Inner.ReadPending(max, cancellationToken);
            return Results.Success<IReadOnlyList<OutboxCommand>>([.. read.Value.Select(c => c with { EnqueuedAt = c.EnqueuedAt.ToOffset(TimeSpan.FromHours(-2)) })]);
        }
    }
}
