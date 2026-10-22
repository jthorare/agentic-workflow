using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Docs;

/// <summary>'step.docs.scope' on the block runtime (docs-sync spec §7.2, W28): the docs that mention a changed path, the docs the design names, and a worktree of their own.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsScopeStepTests
{
    private const string Checkout = "/repos/dev";
    private const string Base = "1111111111111111111111111111111111111111";
    private const string Head = "2222222222222222222222222222222222222222";
    private const string Tree = "/repos/dev/.claude/worktrees/sindri-docs-T-1-docs-sync-1";

    private static readonly string[] _globs = ["docs/**", "*.md"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public FakeWorktrees Worktrees { get; } = new FakeWorktrees().WithRepo(Checkout, Head);

        public FakeChangedPaths Changed { get; } = new();

        public FakeDiffContent Diff { get; } = new();

        public FakeTrackedFiles Tracked { get; } = new FakeTrackedFiles().With(Tree, "README.md", "docs/guide.md", "docs/other.md", "src/Other.cs");

        public InMemoryBriefFiles Files { get; } = new InMemoryBriefFiles()
            .With("README.md", "# Readme\nNothing here.\n")
            .With("docs/guide.md", "# Guide\nThe entry point is src/App.cs.\n")
            .With("docs/other.md", "# Other\nUnrelated.\n")
            .With("src/Other.cs", "// mentions src/App.cs but is not a doc\n");

        public DocsScopeStep Step() => new(new InMemoryRepoCheckouts().With("name", Checkout), Worktrees, Changed, Tracked, Files, Diff);
    }

    private static StepContext Context(string? state = "open", bool enabled = true, string? packet = null, bool withHead = true)
    {
        var head = withHead ? $$"""{"ref":"feat/x","sha":"{{Head}}"}""" : """{"ref":"feat/x"}""";
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pr"] = $$"""{"url":"https://forge.test/o/r/pull/1","state":"{{state}}","base":{"ref":"main","sha":"{{Base}}"},"head":{{head}}}""",
            ["phasePacket"] = packet ?? "{}",
        };
        return BuildShipContexts.Of("step.docs.scope", facts) with
        {
            Repo = RepoName.From("name"),
            Address = "T-1/docs-sync/1",
            Arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["paths"] = JsonSerializer.SerializeToElement(_globs),
                ["enabled"] = JsonSerializer.SerializeToElement(enabled),
            },
        };
    }

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task Lists_the_docs_that_mention_a_changed_path_and_cuts_its_own_worktree()
    {
        var rig = new Rig();
        _ = rig.Changed.With(Checkout, Base, Head, "src/App.cs");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("candidates", result.Result!.Outcome.Value);
        Assert.Equal(Tree, (string?)Out(result, "workspace"));
        var scope = Out(result, "docsScope");
        Assert.Equal(Head, (string?)scope["base"]);
        Assert.Equal(["docs/**", "*.md"], scope["paths"]!.AsArray().Select(static p => (string?)p).ToArray());
        var candidate = Assert.Single(scope["candidates"]!.AsArray());
        Assert.Equal("docs/guide.md", (string?)candidate!["path"]);
        Assert.False((bool?)candidate["designNamed"]);
        Assert.Equal("candidates", (string?)Out(result, "docsResult")["result"]);
        Assert.Equal(Head, (string?)Out(result, "docsResult")["headSha"]);
    }

    [Fact]
    public async Task A_doc_the_design_names_is_listed_and_marked_even_when_nothing_mentions_a_changed_path()
    {
        var rig = new Rig();
        _ = rig.Changed.With(Checkout, Base, Head, "src/App.cs");
        _ = rig.Files.With("design.md", "# Design\nThe change updates docs/other.md and docs/guide.md.\n");

        var result = await rig.Step().Run(Context(packet: """{"hash":"sha256:aa","dir":"/packets/p1"}"""), Token);

        Assert.Equal("candidates", result.Result!.Outcome.Value);
        var named = Out(result, "docsScope")["candidates"]!.AsArray().ToDictionary(static c => (string)c!["path"]!, static c => (bool)c!["designNamed"]!, StringComparer.Ordinal);
        Assert.Equal(new Dictionary<string, bool>(StringComparer.Ordinal) { ["docs/guide.md"] = true, ["docs/other.md"] = true }, named);
        Assert.Equal("sha256:aa", (string?)Out(result, "docsResult")["phasePacket"]);
    }

    [Fact]
    public async Task Reports_none_and_writes_no_workspace_when_no_doc_is_listed()
    {
        var rig = new Rig();
        _ = rig.Changed.With(Checkout, Base, Head, "src/Unmentioned.cs");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("none", result.Result!.Outcome.Value);
        Assert.False(result.Result.Outputs.ContainsKey(FactKey.From("fact:workspace")));
        Assert.Equal("none", (string?)Out(result, "docsResult")["result"]);
    }

    [Fact]
    public async Task A_changed_doc_is_not_a_reason_to_list_another_doc()
    {
        var rig = new Rig();
        _ = rig.Changed.With(Checkout, Base, Head, "docs/guide.md");
        _ = rig.Files.With("docs/other.md", "# Other\nSee docs/guide.md.\n");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("none", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Reports_disabled_without_reading_the_repo()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(enabled: false), Token);

        Assert.Equal("disabled", result.Result!.Outcome.Value);
        Assert.Empty(rig.Changed.Calls);
        Assert.Equal("disabled", (string?)Out(result, "docsResult")["result"]);
    }

    [Theory]
    [InlineData("merged")]
    [InlineData("closed")]
    public async Task Reports_notOpen_for_a_pull_request_that_is_no_longer_open(string state)
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(state), Token);

        Assert.Equal("notOpen", result.Result!.Outcome.Value);
        Assert.Empty(rig.Changed.Calls);
    }

    [Fact]
    public async Task Fails_when_the_pull_request_names_no_head_commit()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(withHead: false), Token);

        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task Fails_when_git_can_not_compare_base_and_head()
    {
        var result = await new Rig().Step().Run(Context(), Token);

        Assert.NotNull(result.Failure);
    }

    private static async Task<string[]> Listed(Rig rig, string guide, params string[] added)
    {
        _ = rig.Changed.With(Checkout, Base, Head, "src/Unmentioned.cs");
        _ = rig.Files.With("docs/guide.md", guide);
        _ = rig.Diff.Adding(Tree, "src/Unmentioned.cs", added);

        var result = await rig.Step().Run(Context(), Token);

        return result.Result!.Outcome.Value == "none"
            ? []
            : [.. Out(result, "docsScope")["candidates"]!.AsArray().Select(static c => (string)c!["path"]!)];
    }

    [Fact]
    public async Task A_doc_naming_a_type_the_diff_adds_is_listed()
        => Assert.Equal(["docs/guide.md"], await Listed(new Rig(), "# Guide\nThe WidgetRegistry holds them.\n", "public sealed class WidgetRegistry : IRegistry"));

    [Fact]
    public async Task A_doc_naming_a_method_the_diff_adds_is_listed()
        => Assert.Equal(["docs/guide.md"], await Listed(new Rig(), "# Guide\nCall RegisterWidgets first.\n", "    public static void RegisterWidgets(int count)"));

    [Fact]
    public async Task A_doc_naming_a_flag_the_diff_adds_is_listed()
        => Assert.Equal(["docs/guide.md"], await Listed(new Rig(), "# Guide\nPass --dry-run to preview.\n", "  if (arg == \"--dry-run\") preview = true;"));

    [Fact]
    public async Task A_doc_naming_an_environment_variable_the_diff_adds_is_listed()
        => Assert.Equal(["docs/guide.md"], await Listed(new Rig(), "# Guide\nSet AW_WIDGET_HOME to move it.\n", "var home = Environment.GetEnvironmentVariable(\"AW_WIDGET_HOME\");"));

    [Fact]
    public async Task A_doc_naming_a_dotted_config_key_the_diff_adds_is_listed()
        => Assert.Equal(["docs/guide.md"], await Listed(new Rig(), "# Guide\nSet workflow.widgets.limit in the profile.\n", "  [\"workflow.widgets.limit\"] = 3,"));

    [Fact]
    public async Task A_short_or_common_name_in_the_diff_does_not_list_a_doc()
        => Assert.Empty(await Listed(new Rig(), "# Guide\nThe Run of a Cache with an id and a -x flag. MAX_N.\n", "class Cache", "void Run(int id)", "if (a == \"-x\") { }", "const MAX_N = 3;"));

    [Fact]
    public async Task Lines_a_changed_doc_adds_are_not_searched_for_names()
    {
        var rig = new Rig();
        _ = rig.Changed.With(Checkout, Base, Head, "docs/other.md");
        _ = rig.Diff.Adding(Tree, "docs/other.md", "Use the WidgetRegistry class.");
        _ = rig.Files.With("docs/guide.md", "# Guide\nThe WidgetRegistry holds them.\n");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("none", result.Result!.Outcome.Value);
    }
}
