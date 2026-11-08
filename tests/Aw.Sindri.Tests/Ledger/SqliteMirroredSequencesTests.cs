using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>Which ledger rows mirror an instance's Temporal events (API spec §3.8, E-30; engine spec §5.6): by the activity id in 'temporal_event_id', within the instance and run the idempotency key names.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteMirroredSequencesTests : IDisposable
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-1/item/1/step.4");
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SequentialIdGenerator _ids = new();
    private readonly LedgerDatabase _database;
    private readonly FakeProcessProbe _probe = new();
    private SindriStores? _stores;
    private LeaseGrant? _grant;

    public SqliteMirroredSequencesTests()
    {
        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<long> Append(string key, string? activity, string instance = "ITM-1/item/1/step.4")
    {
        _stores ??= SqliteStores.Create(_home.Path, _clock, _probe);
        var grant = _grant ??= (await _stores.Lease.Acquire(_probe.Self(), Token)).Value;
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        return (await _stores.Ledger.Append(new LedgerEntry(context.Envelope("workflow.step", $$"""{"instance":"{{instance}}"}""", _clock, _ids), key, Item: null, activity), grant, Token)).Value.Sequence;
    }

    private async Task<IReadOnlyDictionary<string, long>> Of(string run, params string[] activities) =>
        (await new SqliteMirroredSequences(_database).Of(_address, run, activities, Token)).Value;

    [Fact]
    public async Task An_activity_id_finds_the_row_of_its_run_and_instance_and_no_other()
    {
        var mine = await Append("workflow:ITM-1/item/1/step.4:run-1:7", "7");
        _ = await Append("workflow:ITM-1/item/1/step.4:run-2:7", "7");        // another run
        _ = await Append("workflow:ITM-2/item/1/step.4:run-1:7", "7", "ITM-2/item/1/step.4");        // another instance
        _ = await Append("workflow:ITM-1/item/1/step.4:run-1:8", "8");

        var found = await Of("run-1", "7", "9");

        Assert.Equal(new Dictionary<string, long>(StringComparer.Ordinal) { ["7"] = mine }, found);
    }

    [Fact]
    public async Task A_replay_that_recorded_the_activity_again_under_a_content_hash_wins_as_the_newer_row()
    {
        _ = await Append("workflow:ITM-1/item/1/step.4:run-1:7", "7");
        var newer = await Append("workflow:ITM-1/item/1/step.4:run-1:7:ab12", "7");

        Assert.Equal(newer, (await Of("run-1", "7"))["7"]);
    }

    [Fact]
    public async Task A_row_the_engine_did_not_mirror_is_not_found_and_a_ledger_never_written_mirrors_nothing()
    {
        Assert.Empty(await Of("run-1", "7"));   // no ledger file yet

        _ = await Append("workflow:ITM-1/item/1/step.4:run-1:7", activity: null);

        Assert.Empty(await Of("run-1", "7"));
    }

    [Fact]
    public async Task A_run_that_differs_only_in_letter_case_is_another_run()
    {
        _ = await Append("workflow:ITM-1/item/1/step.4:Run-A:7", "7");

        Assert.Empty(await Of("run-a", "7"));
    }

    [Fact]
    public async Task Wildcard_characters_in_a_run_match_only_themselves()
    {
        _ = await Append("workflow:ITM-1/item/1/step.4:run-1:7", "7");

        Assert.Empty(await Of("run_%", "7"));
    }
}
