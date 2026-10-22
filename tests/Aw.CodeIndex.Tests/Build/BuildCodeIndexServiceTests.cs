using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

public sealed class BuildCodeIndexServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // a.cs: Run calls Stop (in b.cs); b.cs: Stop calls Missing (nothing defines it); a call to Twice is ambiguous.
    private static BuildHarness Arranged()
    {
        var harness = new BuildHarness
        {
            Parser = new FakeSymbolParser()
                .With("src/A.cs", new ParseOutcome([Parsed.Class("A", 1), Parsed.Method("A.Run", 2, Parsed.Call("Stop", 3), Parsed.Call("Twice", 4))]))
                .With("src/B.cs", new ParseOutcome([Parsed.Class("B", 1), Parsed.Method("B.Stop", 2, Parsed.Call("Missing", 3)), Parsed.Method("B.Twice", 5), Parsed.Method("C.Twice", 8)])),
        };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A").WithFile(BuildHarness.First, "src/B.cs", "class B").WithFile(BuildHarness.First, "README.md", "# api");
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_build_publishes_the_commit_with_its_counts_and_layers()
    {
        var harness = Arranged();

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        Assert.Equal(BuildHarness.First, report.Commit);
        Assert.Equal(BuildHarness.Name, report.Repo);
        Assert.False(report.Incremental);
        Assert.Equal(2, report.Counts.Files);
        Assert.Equal(6, report.Counts.Symbols);
        Assert.Equal(LayerState.Ok, report.Layers.Single(l => l.Layer == LayerName.Structure).State);
        Assert.Equal(BuildHarness.First, (await harness.Header())!.Meta.Commit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Calls_are_resolved_into_edges_and_the_report_gives_the_unresolved_and_ambiguous_shares()
    {
        var harness = Arranged();

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        // Three call sites: Stop resolves (1), Missing resolves to nothing (1 unresolved), Twice has two candidates (1 ambiguous).
        Assert.Equal(1.0 / 3, report.UnresolvedShare, 3);
        Assert.Equal(1.0 / 3, report.AmbiguousShare, 3);
        Assert.Equal(4, report.Counts.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_left_main_ends_not_on_main_and_changes_nothing()
    {
        var harness = Arranged();
        _ = harness.Heads.WithCommit(BuildHarness.Name, BuildHarness.First, contains: false);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(BuildOutcome.NotOnMain, report.Outcome);
        Assert.Null(report.Commit);
        Assert.Null(await harness.Header());
        Assert.Equal(0, harness.Parser.Parsed);
        Assert.Equal(0, harness.Heavy.Held);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_slot_busy_defers_the_build_as_a_report_not_an_error()
    {
        var harness = new BuildHarness { Heavy = new FakeHeavyJobLock(slots: 0) };

        var result = await harness.TryBuild(BuildHarness.First, Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildOutcome.Deferred, result.Value.Outcome);
        Assert.Null(result.Value.Commit);
        Assert.Null(await harness.Header());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_slot_is_released_after_a_build_that_published_and_after_one_that_failed()
    {
        var harness = Arranged();
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        Assert.Equal(0, harness.Heavy.Held);

        var failed = await harness.TryBuild(BuildHarness.Second, Token);

        Assert.True(failed.IsError);
        Assert.Equal("SND-CODEIDX-002", failed.Error.Code);
        Assert.Equal(0, harness.Heavy.Held);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_commit_builds_incrementally_and_parses_only_the_changed_file()
    {
        var harness = Arranged();
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A { changed }").WithFile(BuildHarness.Second, "src/B.cs", "class B").WithFile(BuildHarness.Second, "README.md", "# api");
        var before = harness.Parser.Parsed;

        var report = await harness.Build(BuildHarness.Second, cancellationToken: Token);

        Assert.True(report.Incremental);
        Assert.Equal(["src/A.cs"], harness.Parser.ParsedPaths.Skip(before), StringComparer.Ordinal);
        Assert.Equal(BuildHarness.Second, (await harness.Header())!.Meta.Commit);
        Assert.Equal(6, report.Counts.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_that_left_the_commit_is_dropped_with_its_symbols()
    {
        var harness = Arranged();
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A");

        var report = await harness.Build(BuildHarness.Second, cancellationToken: Token);

        Assert.Equal(1, report.Counts.Files);
        Assert.Equal(2, report.Counts.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_parser_version_parses_every_file_again()
    {
        var harness = Arranged();
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A").WithFile(BuildHarness.Second, "src/B.cs", "class B");
        var newer = new BuildHarness { Parser = new FakeSymbolParser { Version = "fake-2" }.With("src/A.cs", new ParseOutcome([])).With("src/B.cs", new ParseOutcome([])) };

        // The same store and snapshot, a parser that reports another version.
        var service = new Features.BuildCodeIndex.BuildCodeIndexService(harness.Heads, harness.Snapshot, new Features.BuildCodeIndex.StructureWriter(harness.Snapshot, [newer.Parser], harness.Quarantine, harness.Scrubber), new Features.BuildCodeIndex.BuildLayers(new Features.BuildCodeIndex.EmbeddingsLayer(harness.Embedder, harness.Clock), new Features.BuildCodeIndex.GraphLayer(harness.Snapshot, harness.Clock)), harness.Store, harness.Heavy, harness.Clock);
        var result = await service.Handle(new Features.BuildCodeIndex.BuildCodeIndexRequest(harness.Repo, BuildHarness.Second, new Progress<string>()), Token);

        Assert.Equal(["src/A.cs", "src/B.cs"], newer.Parser.ParsedPaths);
        Assert.Equal(0, result.Value.Counts.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_lines_carry_counts_only()
    {
        var harness = Arranged();
        var lines = new Lines();

        _ = await harness.Build(BuildHarness.First, lines, Token);

        Assert.Contains("structure 2 of 2 files", lines.All, StringComparer.Ordinal);
        Assert.All(lines.All, line => Assert.DoesNotContain("src/", line, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_build_leaves_the_live_index_alone_and_releases_the_slot()
    {
        var harness = Arranged();
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A { changed }").WithFile(BuildHarness.Second, "src/B.cs", "class B { changed }");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        harness.Parser.OnParse = _ => cancel.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Build(BuildHarness.Second, cancellationToken: cancel.Token));

        Assert.Equal(BuildHarness.First, (await harness.Header())!.Meta.Commit);
        Assert.Equal(0, harness.Heavy.Held);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_quarantined_blob_is_stored_unparsed_and_never_given_to_the_parser()
    {
        var harness = Arranged();
        var blob = (await harness.Snapshot.Inventory(harness.Repo, BuildHarness.First, harness.Repo.Settings.SnapshotLimits(), Token)).Value.Files.Single(f => f.Path == "src/B.cs").Blob;
        _ = harness.Quarantine.Recover(BuildHarness.Name);
        harness.Quarantine.Begin(BuildHarness.Name, blob);
        _ = harness.Quarantine.Restarted().Recover(BuildHarness.Name);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(2, report.Counts.Files);
        Assert.DoesNotContain("src/B.cs", harness.Parser.ParsedPaths, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_the_parser_skipped_is_stored_as_not_parsed()
    {
        var harness = Arranged();
        _ = harness.Parser.With("src/B.cs", new ParseOutcome([], Skip: "too-deep"));

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(2, report.Counts.Files);
        Assert.Equal(2, report.Counts.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Signatures_are_scrubbed_before_they_are_stored()
    {
        var harness = new BuildHarness { Scrubber = new FakeBodyScrubber("hunter2") };
        var leaky = Parsed.Method("A.Run", 2) with { Signature = "void Run(string password = \"hunter2\")" };
        _ = harness.Parser.With("src/A.cs", new ParseOutcome([leaky]));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        var hits = (await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery("Run", SymbolMatch.Exact, Repo: null), Token)).Value.Hits;

        Assert.DoesNotContain("hunter2", hits.Single().Signature, StringComparison.Ordinal);
        Assert.Contains("[redacted]", hits.Single().Signature, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Same_name_overloads_in_one_file_get_distinct_refs()
    {
        var harness = new BuildHarness();
        var one = Parsed.Method("A.Run", 2) with { Arity = 1, ParameterTypesText = "int" };
        var two = Parsed.Method("A.Run", 6) with { Arity = 1, ParameterTypesText = "string" };
        var same = Parsed.Method("A.Run", 10) with { Arity = 1, ParameterTypesText = "string" };
        _ = harness.Parser.With("src/A.cs", new ParseOutcome([one, two, same]));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        var refs = (await harness.Facade.FileSymbols(harness.Scope, BuildHarness.Name, ["src/A.cs"], Token)).Value.Files.Single().Symbols.Select(s => s.Ref.Value).ToList();

        Assert.Equal(3, refs.Distinct(StringComparer.Ordinal).Count());
        Assert.All(refs, r => Assert.StartsWith("api:src/A.cs#A.Run/1@", r, StringComparison.Ordinal));
        Assert.Contains(refs, r => r.EndsWith("-2", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Files_that_are_not_source_code_are_not_stored_and_do_not_reach_a_parser()
    {
        var harness = Arranged();

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.DoesNotContain("README.md", harness.Parser.ParsedPaths, StringComparer.Ordinal);
    }
}
