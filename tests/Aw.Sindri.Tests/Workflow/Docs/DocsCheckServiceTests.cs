using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Docs;

/// <summary>'docs check' (docs-sync spec API17): the rules 'step.docs.check' runs, as a service the CLI calls.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsCheckServiceTests
{
    private const string Tree = "/work/docs";

    private static readonly JsonObject _scope = JsonNode.Parse("""{"base":"abc","paths":["docs/**","*.md"],"candidates":[]}""")!.AsObject();

    private static async Task<DocsCheckResponse> Check(FakeRunDiff diff, InMemoryBriefFiles files) =>
        (await new DocsCheckService(diff, files).Handle(new DocsCheckRequest(Tree, _scope), TestContext.Current.CancellationToken)).Value;

    private static FakeRunDiff Changed(params string[] paths)
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree);
        diff.Touch(Tree, paths);
        return diff;
    }

    [Fact]
    public async Task Resolving_docs_pass_with_no_findings()
    {
        var response = await Check(Changed("docs/a.md"), new InMemoryBriefFiles().With("docs/a.md", "# A\nSee [b](b.md).\n").With("docs/b.md", "# B\n"));

        Assert.True(response.Passed);
        Assert.Empty(response.Findings);
    }

    [Fact]
    public async Task A_path_outside_the_globs_a_dead_link_and_an_open_fence_each_name_their_rule()
    {
        var files = new InMemoryBriefFiles().With("docs/a.md", "# A\n[x](gone.md)\n```\n");

        var response = await Check(Changed("docs/a.md", "src/App.cs"), files);

        Assert.False(response.Passed);
        Assert.Equal(["DOCS-LINK-001", "DOCS-MD-001", "DOCS-PATH-001"], response.Findings.Select(static f => f.Rule).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_worktree_with_no_change_fails_with_DOCS_CHANGE_001()
    {
        var response = await Check(Changed(), new InMemoryBriefFiles());

        var finding = Assert.Single(response.Findings);
        Assert.Equal("DOCS-CHANGE-001", finding.Rule);
        Assert.Null(finding.Item);
    }
}
