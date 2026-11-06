using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0030 and what the record store does with a pull request's reviewers, head and reviewed commits (PR review page spec §5.6).</summary>
public sealed class PullFactsStoreTests : IDisposable
{
    private static readonly PullFacts _facts = new(["bob", "cy"], "headC", [new ReviewedCommit("ada", "commitA", 2), new ReviewedCommit("cy", "gone", NewCommits: null)]);

    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static StoredRecord Pr(PullFacts? pull, bool blocked = false) =>
        RecordSamples.Record("github:pr/acme/widgets/12", source: RecordSamples.Github, blocked: blocked) with { Kind = RecordKind.Pr, Pull = pull };

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0030_adds_the_pull_facts_column()
    {
        await _world.Migrate();

        Assert.Equal("0030", ContextDatabase.Migrations[29].Version.ToString("D4", CultureInfo.InvariantCulture));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM pragma_table_info('source_record') WHERE name = 'pull_facts'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_requests_facts_round_trip_through_the_store()
    {
        var pr = Pr(_facts);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(pr, "words")), Ct);

        var read = (await _world.Store.Get(pr.Key, Ct)).Value!;

        Assert.Equal(_facts.RequestedReviewers, read.Pull!.RequestedReviewers);
        Assert.Equal("headC", read.Pull.HeadSha);
        Assert.Equal(_facts.Reviews, read.Pull.Reviews);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_record_without_facts_reads_back_none()
    {
        var pr = Pr(pull: null);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(pr, "words")), Ct);

        Assert.Null((await _world.Store.Get(pr.Key, Ct)).Value!.Pull);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_version_replaces_the_facts()
    {
        var pr = Pr(_facts);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(pr, "words")), Ct);

        var next = pr with { Version = SourceVersion.From("v2"), Pull = new PullFacts([], "headD", [new ReviewedCommit("ada", "headD", 0)]) };
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 2, RecordSamples.Write(next, "words")), Ct);

        var read = (await _world.Store.Get(pr.Key, Ct)).Value!.Pull!;
        Assert.Empty(read.RequestedReviewers);
        Assert.Equal([new ReviewedCommit("ada", "headD", 0)], read.Reviews);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_blocked_pull_request_and_a_tombstone_keep_no_facts()
    {
        var blocked = Pr(_facts, blocked: true);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(blocked)), Ct);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'github:pr/acme/widgets/12' AND pull_facts IS NULL"));

        var live = Pr(_facts) with { Key = RecordKey.From("github:pr/acme/widgets/13") };
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 2, RecordSamples.Write(live, "words")), Ct);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 3) with { Removed = [live.Key] }, Ct);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'github:pr/acme/widgets/13' AND removed_at IS NOT NULL AND pull_facts IS NULL"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0030_makes_the_next_commit_of_an_unchanged_github_pull_request_store_its_facts_and_leaves_the_rest_alone()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath)!);
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(29)], Ct);
        var pr = Pr(_facts);
        _world.Sql($"INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('{pr.Key.Value}', 'github', 'pr', '{pr.Version.Value}', '{pr.TextHash}', '{pr.Ruleset}', 0, '2026-10-27T00:00:00Z', 'Old')");
        _world.Sql("INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('github:issue/acme/widgets/2', 'github', 'issue', 'keep', 'h', 'r', 0, '2026-10-27T00:00:00Z', 'Issue')");
        foreach (var source in new[] { "github", "linear" })
        {
            _world.Sql($"INSERT INTO source_cursor (source, repo, cursor, last_sync, phase) VALUES ('{source}', 'records', 'mark', '2026-10-27T00:00:00Z', 'incremental')");
        }

        // why: 0031 and 0032 reset the github and linear feeds again on purpose; this pins what 0030 alone leaves
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(30)], Ct);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'github:pr/acme/widgets/12' AND version = ''"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'github:issue/acme/widgets/2' AND version = 'keep'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'github' AND repo = 'records' AND phase = 'recent' AND cursor IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear' AND repo = 'records' AND phase = 'incremental' AND cursor = 'mark'"));

        var commit = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(pr, "words")), Ct);

        Assert.Equal(1, commit.Value.Written);
        Assert.Equal("headC", (await _world.Store.Get(pr.Key, Ct)).Value!.Pull!.HeadSha);
    }
}
