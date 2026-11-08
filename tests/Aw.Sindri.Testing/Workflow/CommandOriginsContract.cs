using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="ICommandOrigins"/> honours (engine spec rev 8 §5.4): the command a reply names as its
/// causation, found by its envelope id, names the instance it was written for; a reply without a causation, a causation
/// no event has, or a command that names no instance, has none.
/// </summary>
public abstract class CommandOriginsContract
{
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>The probe; this process is pid 100.</summary>
    protected FakeProcessProbe Probe { get; } = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The lease, the ledger and the origins over it, on a fresh file or memory.</summary>
    /// <param name="clock">The only clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <returns>The stores and the origins that read their ledger.</returns>
    protected abstract ValueTask<(SindriStores Stores, ICommandOrigins Origins)> Create(TimeProvider clock, IProcessProbe probe);

    /// <summary>A reply to a command recorded for an instance names that instance.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reply_names_the_instance_its_command_was_written_for()
    {
        var (stores, origins) = await Create(Clock, Probe);
        var grant = (await stores.Lease.Acquire(Probe.Self(), Token)).Value;
        var command = await Record(stores.Ledger, grant, "workflow.started", """{"instance":"T-1/item/1","from":"","to":"triaged"}""", causation: null);
        _ = await Record(stores.Ledger, grant, "workflow.started", """{"instance":"T-2/item/1","from":"","to":"triaged"}""", causation: null);
        var reply = await Record(stores.Ledger, grant, "claim.created", """{"outcome":"created"}""", command.Envelope.Id);

        var found = await origins.CauserOf(reply, Token);

        Expect.Equal(InstanceAddress.From("T-1/item/1"), found.Value);
    }

    /// <summary>A reply with no causation, with a causation no event has, or caused by an event that names no instance, has none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reply_without_a_recorded_causer_has_none()
    {
        var (stores, origins) = await Create(Clock, Probe);
        var grant = (await stores.Lease.Acquire(Probe.Self(), Token)).Value;
        var heartbeat = await Record(stores.Ledger, grant, "service.heartbeat", """{"slot":"2026-01-01T00:00:00Z"}""", causation: null);
        var orphan = await Record(stores.Ledger, grant, "claim.created", """{"outcome":"created"}""", causation: null);
        var unknown = await Record(stores.Ledger, grant, "claim.created", """{"outcome":"created"}""", _ids.NewId());
        var nameless = await Record(stores.Ledger, grant, "claim.created", """{"outcome":"created"}""", heartbeat.Envelope.Id);

        var found = new[] { await origins.CauserOf(orphan, Token), await origins.CauserOf(unknown, Token), await origins.CauserOf(nameless, Token) };

        Expect.True(found.All(static f => !f.IsError && f.Value is null));
    }

    private async Task<LedgerEvent> Record(ILedgerStore ledger, LeaseGrant grant, string type, string payload, Guid? causation)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId(), traceId: null).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        var envelope = context.Envelope(type, payload, Clock, _ids) with { CausationId = causation };
        var appended = await ledger.Append(new LedgerEntry(envelope, "k:" + envelope.Id.ToString("N"), "T-1"), grant, Token);
        Expect.True(!appended.IsError);
        return new LedgerEvent(appended.Value.Sequence, grant.Epoch, envelope, "k:" + envelope.Id.ToString("N"), "T-1");
    }
}
