using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.pr.context' (PB spec §10.1): the PR's state, changed paths, failing check excerpts and the fenced comment thread, scrubbed, never failing the fill for a forge error.</summary>
[Lane(TestLane.Unit)]
public sealed class PrContextInputTests
{
    private static readonly PullRepo _repo = new("forge.test", "acme", "widgets");

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("hunter2", "[redacted]", StringComparison.Ordinal);
    }

    private static StepInputContext Context(string? link, string? workspace = null, string? prState = null)
    {
        var facts = new Dictionary<FactKey, JsonElement>();
        if (link is not null)
        {
            facts[FactKey.From("fact:pr")] = JsonSerializer.SerializeToElement(link);
        }

        if (workspace is not null)
        {
            facts[FactKey.From("fact:workspace")] = JsonSerializer.SerializeToElement(workspace);
        }

        if (prState is not null)
        {
            facts[FactKey.From("fact:prState")] = JsonSerializer.SerializeToElement(prState);
        }

        return new StepInputContext(BlockId.From(PrContextInput.Id), BlockId.From("step.pr.ci-fix"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts);
    }

    private static PrContextInput Input(FakePullRequests pulls, FakeChangedPaths? paths = null) =>
        new(pulls, paths ?? new FakeChangedPaths(), new Redacting(), new ContextInputFixtures.MemoryFacts());

    private static async Task<string> Text(PrContextInput input, StepInputContext context)
    {
        var filled = await input.Fill(context, TestContext.Current.CancellationToken);
        Assert.False(filled.IsError);
        return filled.Value.Sections[PrContextInput.Section];
    }

    [Fact]
    public async Task Serves_failing_check_logs_and_the_fenced_scrubbed_thread()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        var pull = pulls.Add(_repo, "feat/x", new string('a', 40));
        pull.FailingChecks.Add("build");
        _ = pulls.SeedThread(pull.Link, "please drop hunter2 from the config");

        var text = await Text(Input(pulls), Context(pull.Link));

        Assert.Contains("build", text, StringComparison.Ordinal);
        Assert.Contains("log of build", text, StringComparison.Ordinal);
        Assert.Contains("please drop [redacted] from the config", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.Contains("untrusted", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lists_the_changed_paths_between_the_prs_base_and_head_in_the_workspace()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        var pull = pulls.Add(_repo, "feat/x", "headsha");
        var paths = new FakeChangedPaths().With("/work/web", "main", "headsha", "src/a.cs", "docs/b.md");

        var text = await Text(Input(pulls, paths), Context(pull.Link, "/work/web", "{\"title\":\"Add a\",\"body\":\"why\",\"base\":\"main\"}"));

        Assert.Contains("src/a.cs", text, StringComparison.Ordinal);
        Assert.Contains("docs/b.md", text, StringComparison.Ordinal);
        Assert.Contains("Add a", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Caps_each_check_excerpt()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        var pull = pulls.Add(_repo, "feat/x", new string('a', 40));
        pull.FailingChecks.Add(new string('n', 6000));

        var text = await Text(Input(pulls), Context(pull.Link));

        Assert.True(text.Length < 20000);
    }

    [Fact]
    public async Task A_forge_error_is_an_unavailable_section_and_the_fill_succeeds()
    {
        var pulls = new FakePullRequests(["forge.test"]);

        var text = await Text(Input(pulls), Context("https://forge.test/acme/widgets/pull/99"));

        Assert.Contains("unavailable", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_fact_pr_it_says_so()
    {
        var text = await Text(Input(new FakePullRequests([])), Context(link: null));

        Assert.Contains("unavailable", text, StringComparison.Ordinal);
    }
}
