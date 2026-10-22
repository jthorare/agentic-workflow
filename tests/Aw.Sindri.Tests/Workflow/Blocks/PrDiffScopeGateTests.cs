using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Blocks.PrGateTestInputs;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>'gate.pr.diff-scope' (PB spec §10.1, BD2, BD5): every changed path is in scope, no CI config changes, no test weakened.</summary>
public sealed class PrDiffScopeGateTests
{
    private const string Packet = """{"allowedFiles":["src/Feature/**","new: src/Other/New.cs"]}""";

    [Fact, Trait("Lane", "unit")]
    public async Task Paths_in_the_packets_allowed_files_and_their_tests_pass()
    {
        var result = await Phase(Packet, push: ["src/Feature/A.cs", "src/Other/New.cs", "tests/Feature/ATests.cs"]);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_outside_the_allowed_files_refuses_and_names_it()
    {
        var result = await Phase(Packet, push: ["src/Feature/A.cs", "src/Unrelated/B.cs"]);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("src/Unrelated/B.cs", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".gitlab-ci.yml")]
    [InlineData("azure-pipelines.yml")]
    public async Task A_changed_CI_config_refuses_even_when_the_packet_allows_everything(string path)
    {
        var result = await Phase("""{"allowedFiles":["**"]}""", push: [path]);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains(path, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deleted_test_is_a_weakened_test()
    {
        var result = await Phase(Packet, push: ["tests/Feature/ATests.cs"], deleted: ["tests/Feature/ATests.cs"]);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("tests/Feature/ATests.cs", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_skip_added_to_a_test_is_a_weakened_test()
    {
        var content = new FakeDiffContent().Adding(Workspace, "tests/Feature/ATests.cs", "[Fact(Skip = \"flaky\")]");

        var result = await Phase(Packet, push: ["tests/Feature/ATests.cs"], content: content);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("tests/Feature/ATests.cs:1", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_packet_falls_back_to_the_files_the_PR_already_changes()
    {
        var paths = new FakeChangedPaths()
            .With(Workspace, "b0", "h1", "src/Pr/Own.cs")
            .With(Workspace, "h1", "HEAD", "src/Pr/Own.cs", "src/Elsewhere/X.cs");

        var result = await Evaluate("""{"scope":"phase","phase":{}}""", paths, new FakeDiffContent().Add(Workspace));

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("src/Elsewhere/X.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Reasons, r => r.Contains("src/Pr/Own.cs", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Docs_scope_allows_only_the_doc_globs_and_the_docs_the_design_names()
    {
        var paths = new FakeChangedPaths().With(Workspace, "h1", "HEAD", "docs/a.md", "design/named.md", "src/App.cs");
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:docsScope")] = Json("""{"candidates":[{"path":"design/named.md","designNamed":true}]}"""),
        };

        var result = await Evaluate("""{"scope":"docs","paths":["docs/**"],"phase":{}}""", paths, new FakeDiffContent().Add(Workspace), facts);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        var reasons = string.Join('\n', result.Reasons);
        Assert.Contains("src/App.cs", reasons, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/a.md", reasons, StringComparison.Ordinal);
        Assert.DoesNotContain("design/named.md", reasons, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_workspace_it_cannot_read_refuses()
    {
        var result = await Evaluate("""{"scope":"phase","phase":{}}""", new FakeChangedPaths(), new FakeDiffContent());

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    private static Task<GateResult> Phase(string packet, string[] push, string[]? deleted = null, FakeDiffContent? content = null)
    {
        var paths = new FakeChangedPaths().With(Workspace, "h1", "HEAD", push);
        content ??= new FakeDiffContent().Add(Workspace);
        if (deleted is not null)
        {
            _ = content.Deleting(Workspace, deleted);
        }

        return Evaluate($$"""{"scope":"phase","phase":{{packet}}}""", paths, content);
    }

    private static async Task<GateResult> Evaluate(string with, FakeChangedPaths paths, FakeDiffContent content, Dictionary<FactKey, JsonElement>? extraFacts = null)
    {
        var arguments = JsonDocument.Parse(with).RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.Clone(), StringComparer.Ordinal);
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:workspace")] = Json($"\"{Workspace}\""),
            [FactKey.From("fact:prState")] = Json(PrState()),
        };
        foreach (var (key, value) in extraFacts ?? [])
        {
            facts[key] = value;
        }

        return await new PrDiffScopeGate(paths, content).Evaluate(GateOf("gate.pr.diff-scope", arguments, facts), TestContext.Current.CancellationToken);
    }
}
