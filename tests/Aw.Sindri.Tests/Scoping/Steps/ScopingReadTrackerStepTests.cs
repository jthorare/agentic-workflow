using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.read-tracker' on the block runtime (scoping spec §4, §8.2): the record through the tracker port, scrubbed, the text shape's anchors, then the desk helper's code.</summary>
public sealed class ScopingReadTrackerStepTests
{
    private static readonly RepoName _web = RepoName.From("web");

    private static TrackerSeed Seed() => new(
        [new TrackerSeedProject("Notes app", "A small notes app.")],
        [
            new TrackerSeedIssue("NTS-2", "Links expire", "Shared links expire after a chosen time.", "Notes app", [new TrackerComment("Rowan", "Use the owner's timezone. key=AKIA-SECRET-1")]),
            new TrackerSeedIssue("NTS-3", "Digest", "A weekly digest.", "Notes app", []),
            new TrackerSeedIssue("NTS-4", "Blank", string.Empty, Project: null, []),
        ]);

    private static ScopeReadTrackerStep Step(ITrackerSource tracker, InMemoryScopingTextShape shape) =>
        new(tracker, shape, new MaskingScrubber(), new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero)));

    private static StepContext Context(string scopeInput, RepoName? repo = null) =>
        ScopingStepContexts.Of("step.scope.read-tracker", new ScopingFacts { ["scopeInput"] = scopeInput }) with { Repo = repo ?? _web, Address = "T-1/scoping/1" };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static string Input(string key) => $$"""{"kind":"tracker","repo":"web","key":"{{key}}"}""";

    [Fact, Lane(TestLane.Unit)]
    public async Task An_issue_is_the_brief_with_its_comments_scrubbed_and_the_text_shapes_anchors()
    {
        var shape = new InMemoryScopingTextShape(["feature:links"]);

        var result = await Step(new InMemoryTrackerSource(Seed()), shape).Run(Context(Input("NTS-2")), TestContext.Current.CancellationToken);

        Assert.Equal("read", result.Result!.Outcome.Value);
        var brief = Out(result, "brief");
        var text = brief["text"]!.GetValue<string>();
        Assert.Contains("Shared links expire after a chosen time.", text, StringComparison.Ordinal);
        Assert.Contains("Use the owner's timezone.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AKIA-SECRET-1", text, StringComparison.Ordinal);
        Assert.Equal("Links expire", brief["subject"]!.GetValue<string>());
        var reference = brief["refs"]![0]!;
        Assert.Equal(("R1", "tracker", "NTS-2", TrackerSeed.Updated, "untrusted", "2026-10-20T09:00:00Z"),
            (reference["id"]!.GetValue<string>(), reference["source"]!.GetValue<string>(), reference["key"]!.GetValue<string>(), reference["version"]!.GetValue<string>(), reference["trust"]!.GetValue<string>(), reference["fetchedAt"]!.GetValue<string>()));
        Assert.Equal(["feature:links"], Out(result, "anchors").AsArray().Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
        var (repo, asked) = Assert.Single(shape.Asked);
        Assert.Equal((_web, text), (repo!.Value, asked));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_project_brief_lists_its_issues()
    {
        var result = await Step(new InMemoryTrackerSource(Seed()), new InMemoryScopingTextShape([])).Run(Context(Input("Notes app")), TestContext.Current.CancellationToken);

        var text = Out(result, "brief")["text"]!.GetValue<string>();
        Assert.Contains("NTS-2", text, StringComparison.Ordinal);
        Assert.Contains("A weekly digest.", text, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_issue_with_no_description_and_no_comments_is_empty_and_never_asks_the_engine()
    {
        var shape = new InMemoryScopingTextShape(["feature:x"]);

        var result = await Step(new InMemoryTrackerSource(Seed()), shape).Run(Context(Input("NTS-4")), TestContext.Current.CancellationToken);

        Assert.Equal("empty", result.Result!.Outcome.Value);
        Assert.Empty(shape.Asked);
    }

    [Theory, Lane(TestLane.Unit)]
    [InlineData("""{"kind":"tracker","repo":"web"}""", "key")]
    [InlineData("""{"kind":"brief","repo":"web","path":"a.md"}""", "tracker")]
    public async Task A_scope_input_that_is_not_a_tracker_with_a_key_fails_the_Step(string input, string mentions)
    {
        var result = await Step(new InMemoryTrackerSource(Seed()), new InMemoryScopingTextShape([])).Run(Context(input), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains(mentions, result.Failure, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task No_tracker_configured_fails_the_Step_with_the_code()
    {
        var result = await Step(InMemoryTrackerSource.Unconfigured, new InMemoryScopingTextShape([])).Run(Context(Input("NTS-2")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("SND-SCOPING-005", result.Failure, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_key_that_names_nothing_fails_the_Step()
    {
        var result = await Step(new InMemoryTrackerSource(Seed()), new InMemoryScopingTextShape([])).Run(Context(Input("NTS-99")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("NTS-99", result.Failure, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task The_engine_unable_to_answer_fails_the_Step()
    {
        var result = await Step(new InMemoryTrackerSource(Seed()), new InMemoryScopingTextShape(SindriErrors.InvalidInput.With("no map"))).Run(Context(Input("NTS-2")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("no map", result.Failure, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task The_fixtures_expiry_issue_reads_through_the_file_adapter()
    {
        var result = await Step(new FileTrackerSource(FileTrackerSourceFixtureTests.Export()), new InMemoryScopingTextShape(["feature:links"])).Run(Context(Input("NTS-135")), TestContext.Current.CancellationToken);

        Assert.Equal("read", result.Result!.Outcome.Value);
        Assert.Equal("NTS-135", Out(result, "brief")["refs"]![0]!["key"]!.GetValue<string>());
    }

    private sealed class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("AKIA-SECRET-1", "[redacted]", StringComparison.Ordinal);
    }
}
