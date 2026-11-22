using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Evolve;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Proposals;

/// <summary>The 'proposal' tables of ledger.db (migration 23): save, the merge of the same proposal, reject and the rejected-before rule, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteProposalStoreTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly LedgerDatabase _database;
    private readonly SqliteProposalStore _store;

    public SqliteProposalStoreTests()
    {
        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);
        _store = new SqliteProposalStore(_database, new SequentialIdGenerator(), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    [Fact]
    public void The_migration_that_makes_the_tables_is_named_proposals() =>
        Assert.Contains(LedgerDatabase.Migrations, static m => m.Name.Contains("proposals", StringComparison.Ordinal));

    [Fact]
    public async Task A_saved_proposal_reads_back_proposed_with_its_original_and_sessions()
    {
        var saved = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;

        var found = (await _store.Find(saved.Id, Token)).Value;

        Assert.Equal(ProposalSaveOutcome.Saved, saved.Outcome);
        Assert.NotNull(found);
        Assert.Equal(ProposalState.Proposed, found.State);
        Assert.Equal(ProposalTier.Code, found.Tier);
        Assert.Equal(0, found.Attempts);
        Assert.Equal(ProposalSeeds.Original("Run the tests before saying done").Title, found.Original.Title);
        Assert.Equal(["skills/review/SKILL.md"], found.Original.Change.Files);
        Assert.Equal(["transcript:1a2b3c4d"], found.Sessions);
    }

    [Fact]
    public async Task The_same_title_in_other_case_and_punctuation_merges_its_evidence_and_sessions()
    {
        var first = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;
        var again = ProposalSeeds.Saving("run the tests, before saying DONE!", session: "transcript:9f8e7d6c") with
        {
            Original = ProposalSeeds.Original("run the tests, before saying DONE!", evidence: "transcript:9f8e7d6c#4"),
        };

        var merged = (await _store.Save(again, Token)).Value;
        var found = (await _store.Find(first.Id, Token)).Value;

        Assert.Equal(new ProposalSaved(first.Id, ProposalSaveOutcome.Merged), merged);
        Assert.NotNull(found);
        Assert.Equal(["transcript:1a2b3c4d#12", "transcript:9f8e7d6c#4"], found.Original.Evidence);
        Assert.Equal(["transcript:1a2b3c4d", "transcript:9f8e7d6c"], found.Sessions);
        _ = Assert.Single((await _store.List(state: null, Token)).Value);
    }

    [Fact]
    public async Task A_proposal_saved_with_a_draft_reads_it_back_and_one_without_reads_null()
    {
        var draft = new ProposalDraft("wf/steering-miner/run_1/work", "0123456789abcdef0123456789abcdef01234567");
        var with = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done") with { Draft = draft }, Token)).Value;
        var without = (await _store.Save(ProposalSeeds.Saving("Ask before widening the scope"), Token)).Value;

        Assert.Equal(draft, (await _store.Find(with.Id, Token)).Value?.Draft);
        Assert.Null((await _store.Find(without.Id, Token)).Value?.Draft);
    }

    [Fact]
    public async Task A_merge_keeps_the_newer_draft_and_a_merge_without_one_keeps_the_old()
    {
        var older = new ProposalDraft("wf/steering-miner/run_1/work", "1111111111111111111111111111111111111111");
        var newer = new ProposalDraft("wf/steering-miner/run_2/work", "2222222222222222222222222222222222222222");
        var first = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done") with { Draft = older }, Token)).Value;

        _ = await _store.Save(ProposalSeeds.Saving("Run the tests before saying done") with { Draft = newer }, Token);
        var afterNewer = (await _store.Find(first.Id, Token)).Value?.Draft;
        _ = await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token);
        var afterNone = (await _store.Find(first.Id, Token)).Value?.Draft;

        Assert.Equal(newer, afterNewer);
        Assert.Equal(newer, afterNone);
    }

    [Fact]
    public async Task The_same_title_for_another_target_is_a_new_proposal()
    {
        _ = await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token);

        var other = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done", target: "skill:judge"), Token)).Value;

        Assert.Equal(ProposalSaveOutcome.Saved, other.Outcome);
        Assert.Equal(2, (await _store.List(state: null, Token)).Value.Count);
    }

    [Fact]
    public async Task A_rejected_proposal_is_never_saved_again()
    {
        var first = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;
        var rejected = (await _store.Reject(first.Id, Token)).Value;

        var again = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;

        Assert.Equal(ProposalState.Rejected, rejected.State);
        Assert.Equal(new ProposalSaved(first.Id, ProposalSaveOutcome.PreviouslyRejected), again);
        _ = Assert.Single((await _store.List(state: null, Token)).Value);
    }

    [Fact]
    public async Task Rejecting_twice_is_not_rejectable_and_an_unknown_id_is_not_found()
    {
        var first = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;
        _ = await _store.Reject(first.Id, Token);

        var twice = await _store.Reject(first.Id, Token);
        var unknown = await _store.Reject(ProposalId.From("prp_" + new string('0', 32)), Token);

        Assert.Equal(SindriErrors.ProposalNotRejectable.Code, twice.Error.Code);
        Assert.Equal(SindriErrors.ProposalNotFound.Code, unknown.Error.Code);
    }

    [Fact]
    public async Task The_list_filters_by_state_and_puts_the_newest_change_first()
    {
        var older = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done"), Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(1));
        var newer = (await _store.Save(ProposalSeeds.Saving("Ask before widening the scope"), Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _store.Reject(older.Id, Token);

        var all = (await _store.List(state: null, Token)).Value;
        var proposed = (await _store.List(ProposalState.Proposed, Token)).Value;

        Assert.Equal([older.Id, newer.Id], all.Select(static p => p.Id));
        Assert.Equal([newer.Id], proposed.Select(static p => p.Id));
    }

    [Fact]
    public async Task A_source_session_seen_again_keeps_its_turns_and_gains_new_ones()
    {
        var first = (await _store.Save(ProposalSeeds.Saving("Run the tests before saying done", turns: ["one", "two"]), Token)).Value;
        _ = await _store.Save(ProposalSeeds.Saving("Run the tests before saying done", turns: ["two", "three"]), Token);

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_home.Path, "ledger.db")};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT turns FROM proposal_source WHERE proposal_id = $id";
        _ = command.Parameters.AddWithValue("$id", first.Id.Value);
        Assert.Equal("""["one","two","three"]""", (string?)await command.ExecuteScalarAsync(Token));
    }
}
