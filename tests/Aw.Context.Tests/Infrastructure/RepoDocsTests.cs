using System.Diagnostics;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Testing.Fixtures;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Infrastructure;

public sealed class RepoDocsTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_planted_secret_never_reaches_the_index_and_an_edit_reads_as_a_new_version()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var doc = Path.Combine(fixture.Path, "docs", "architecture.md");
        await File.AppendAllTextAsync(doc, "\n## Deploy\n\nThe orientation deploy key is " + Planted.Secret + "\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "plant");

        var first = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock);
        var hits = (await source.Search(new SourceQuery(["deploy"], AsOf: null, 10, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value;
        await File.AppendAllTextAsync(doc, "\nedited\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "edit");
        var versions = (await source.Versions([hits[0].Ref], CancellationToken.None)).Value;

        Assert.Equal(2, first.Added);
        Assert.Equal(0L, Count(home, $"SELECT count(*) FROM doc_chunk WHERE text LIKE '%{Planted.Secret}%'"));
        Assert.Equal(1L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%[REDACTED:github-token]%'"));
        Assert.Equal("repo:split-panes/docs/architecture.md", hits[0].Ref.Canonical.Value);
        Assert.Equal(Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md"), versions[0]?.Value);
        Assert.NotEqual(hits[0].Ref.Version, versions[0]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_pattern_set_cleans_stored_chunks_again_and_nothing_cleaned_with_the_old_rules_is_a_hit()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        await File.AppendAllTextAsync(Path.Combine(fixture.Path, "docs", "architecture.md"), "\n## Customer\n\nThe zebra customer is ACME-123456.\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "customer");
        var acme = home.Factory.For([new("acme-customer-id", "ACME-[0-9]{6}")]).Value;
        var zebra = new SourceQuery(["zebra"], AsOf: null, 10, TimeSpan.FromSeconds(5));

        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var rawRows = Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%ACME-123456%'");
        var underOld = (await new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock).Search(zebra, CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, acme, home.Clock);
        var beforeRefresh = (await source.Search(zebra, CancellationToken.None)).Value;
        var refreshed = (await home.Index.Refresh(repo, ["docs/**/*.md"], acme, CancellationToken.None)).Value;
        var afterRefresh = (await source.Search(zebra, CancellationToken.None)).Value;
        var fetched = (await source.Fetch(afterRefresh.Single().Ref, CancellationToken.None)).Value;

        Assert.Equal((1L, 1), (rawRows, underOld.Count));   // no rule knew the id: stored as written, a hit under those rules
        Assert.Empty(beforeRefresh);                          // stored under the old rules: never a hit under the new ones
        Assert.Equal((2, 2, 0), (refreshed.Recleaned, refreshed.Added, refreshed.Kept));
        Assert.Equal(0L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%ACME-123456%'"));
        Assert.Equal(1L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%[REDACTED:acme-customer-id]%'"));
        Assert.Equal(1L, Count(home, $"SELECT count(DISTINCT ruleset) FROM doc WHERE ruleset = '{acme.Ruleset.Value}'"));
        Assert.Contains("The zebra customer is [REDACTED:acme-customer-id].", fetched.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Rows_an_S1A_store_wrote_are_outdated_after_the_upgrade_and_cleaned_again()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var blob = Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md");
        Directory.CreateDirectory(home.Settings.Home);
        var connections = new Kernel.Data.SqliteConnectionFactory(new Kernel.Data.SqliteOptions(home.Settings.DatabasePath));
        Assert.True((await new Kernel.Data.MigrationRunner(connections, home.Clock).Migrate(Context.Infrastructure.Store.ContextDatabase.Migrations,
            TestContext.Current.CancellationToken, targetVersion: 2)).IsSuccess);
        using (var raw = new SqliteConnection($"Data Source={home.Settings.DatabasePath};Pooling=False"))
        {
            raw.Open();
            using var insert = raw.CreateCommand();
            insert.CommandText = "INSERT INTO doc (repo, path, blob, title, blocked) VALUES ('split-panes', 'docs/architecture.md', $blob, 'Architecture', 0); " +
                "INSERT INTO doc_chunk (repo, path, blob, line_start, line_end, text) VALUES ('split-panes', 'docs/architecture.md', $blob, 1, 2, 'zebra orientation');";
            _ = insert.Parameters.AddWithValue("$blob", blob);
            _ = insert.ExecuteNonQuery();
        }

        var outdated = (await home.Index.Status(repo, home.Scrubbers.Ruleset, CancellationToken.None)).Value;
        var hits = (await new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock)
            .Search(new SourceQuery(["zebra"], AsOf: null, 10, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value;
        var refreshed = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;

        Assert.Equal((1, 1), (outdated.Documents, outdated.Outdated));   // migration 0003 gave the S-1A row ruleset ''
        Assert.Empty(hits);
        Assert.Equal((1, 2), (refreshed.Recleaned, refreshed.Added));
        Assert.Equal(0L, Count(home, $"SELECT count(*) FROM doc WHERE ruleset <> '{home.Scrubbers.Ruleset.Value}'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_git_cant_read_is_reported_with_its_code_and_a_source_outside_scope_is_not_found()
    {
        using var home = new TempHome();
        var missing = new ContextRepo(ContextRepoName.From("gone"), Path.Combine(Path.GetTempPath(), "aw-ctx-no-such-repo"));

        var refreshed = (await home.Index.Refresh(missing, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var status = (await home.Index.Status(missing, home.Scrubbers.Ruleset, CancellationToken.None)).Value;
        var fetched = await new RepoDocsSource([], home.Database, home.Git, home.Scrubbers, home.Clock).Fetch(
            new SourceRef(SourceId.From("repo-docs"), ExternalId.From("gone/docs/a.md#L1-L2"), SourceVersion.From("abcdef1"), Permalink: null,
                RecordKey.From("repo:gone/docs/a.md")), CancellationToken.None);

        Assert.Equal("SND-CONTEXT-010", refreshed.Code);
        Assert.Null(refreshed.Head);
        Assert.Equal("SND-CONTEXT-010", status.Code);
        Assert.Equal("SND-CONTEXT-003", fetched.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_key_or_a_hidden_comment_across_a_chunk_boundary_never_reaches_the_index_or_a_fetch()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var body = Enumerable.Range(0, 30).Select(i => new string((char)('A' + (i % 26)), 60) + "QZ" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var key = string.Join('\n', ["-----BEGIN " + "RSA PRIVATE KEY-----", .. body, "-----END " + "RSA PRIVATE KEY-----"]);
        var filler = string.Join('\n', Enumerable.Range(0, 30).Select(i => "Orientation filler line " + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var doc = "# Keys\n" + filler + "\n" + key + "\n<!--\n# a heading inside a comment\nhidden-instruction-text\n-->\nend orientation\n";
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "keys.md"), doc, TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "keys");

        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock);
        var hits = (await source.Search(new SourceQuery(["orientation", "filler"], AsOf: null, 50, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value;
        var fetched = new List<string>();
        foreach (var hit in hits.Where(h => h.Ref.Canonical.Value.EndsWith("keys.md", StringComparison.Ordinal)))
        {
            fetched.Add((await source.Fetch(hit.Ref, CancellationToken.None)).Value.Text);
        }

        Assert.True(Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/keys.md'") >= 2);   // the doc spans chunks
        Assert.Equal(0L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%QZ1%' OR text LIKE '%hidden-instruction%'"));
        Assert.DoesNotContain(fetched, t => t.Contains("QZ1", StringComparison.Ordinal) || t.Contains("hidden-instruction", StringComparison.Ordinal));
        Assert.NotEmpty(fetched);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_doc_over_a_mebibyte_is_searchable_and_an_unreadable_one_keeps_its_earlier_rows()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "huge.md"), "# Huge\n" + new string('x', 1_100_000), TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(Path.Combine(fixture.Path, "docs", "architecture.md"), "\nA later line.\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "huge and edit");
        var newBlob = Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md");
        File.Delete(Path.Combine(fixture.Path, ".git", "objects", newBlob[..2], newBlob[2..]));   // git can't read the edit's blob this run

        var refreshed = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var again = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;

        Assert.Equal(1, refreshed.Skipped);   // only the unreadable edit: a doc has no size limit (owner q93, 2026-10-23)
        Assert.Equal(1L, Count(home, "SELECT count(*) FROM doc WHERE path = 'docs/huge.md'"));
        Assert.True(Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/huge.md'") > 0);
        Assert.True(Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/architecture.md'") > 0);   // its earlier rows stay
        Assert.Equal(1, again.Skipped);   // the edit is still unreadable
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Git_maps_a_missing_object_to_not_found_and_an_unreadable_repo_to_unavailable()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();

        var missing = await home.Git.Size(fixture.Path, new string('0', 40), CancellationToken.None);
        var noRepo = await home.Git.Head(Path.Combine(fixture.Path, "no-such-dir"), CancellationToken.None);

        Assert.Equal(("SND-CONTEXT-003", Kernel.ErrorKind.NotFound), (missing.Error.Code, missing.Error.Kind));
        Assert.Equal("SND-CONTEXT-010", noRepo.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_fetched_span_still_matches_the_blob_s_lines_after_a_comment_was_removed()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var doc = "# Notes\n<!--\nhidden\nlines\n-->\nintro\n\n## Orientation\nthe zebra line\nlast line\n";
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "notes.md"), doc, TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "notes");
        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock);

        var hit = (await source.Search(new SourceQuery(["zebra"], AsOf: null, 5, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value.Single();
        var record = (await source.Fetch(hit.Ref, CancellationToken.None)).Value;

        Assert.EndsWith("#L8-L11", hit.Ref.Id.Value, StringComparison.Ordinal);
        Assert.StartsWith("## Orientation\nthe zebra line\nlast line", record.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_doc_path_the_ids_can_t_carry_is_skipped_and_never_breaks_a_query()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "team\u200Bnotes.md"), "# Notes\norientation zebra\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "an odd name");

        var refreshed = (await home.Index.Refresh(repo, ["docs/**/*.md"], home.Scrubbers, CancellationToken.None)).Value;
        var hits = await new RepoDocsSource([repo], home.Database, home.Git, home.Scrubbers, home.Clock)
            .Search(new SourceQuery(["orientation"], AsOf: null, 10, TimeSpan.FromSeconds(5)), CancellationToken.None);

        Assert.Equal(1, refreshed.Skipped);
        Assert.True(hits.IsSuccess);
        Assert.DoesNotContain(hits.Value, h => h.Ref.Id.Value.Contains("notes", StringComparison.Ordinal));
    }

    private static long Count(TempHome home, string sql)
    {
        using var connection = new SqliteConnection("Data Source=" + home.Settings.DatabasePath + ";Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;   // why: count(*) always answers a row
    }

    private static string Git(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", repo, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
