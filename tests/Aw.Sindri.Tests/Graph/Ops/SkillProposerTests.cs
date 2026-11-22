using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Infrastructure.Workflow.Code;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.Workflow.Code;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>
/// The 'skill-proposer' starter (issue 3787, piece 4, PR 1): its graph, the draft prompt's rules, and its three code bodies run on the inputs the engine renders
/// from their config. Each body is the exact text the template ships, compiled by the real compiler and run in process.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SkillProposerTests : IClassFixture<RoslynWarmup>
{
    private static readonly JsonArray _skills = [Skill("review"), Skill("bugHunt"), Skill("syncDocs")];

    /// <summary>Takes the warm-up so xUnit builds it before the first fact; the fixture's work is done in its constructor.</summary>
    /// <param name="warmup">The warm-up, already run.</param>
    public SkillProposerTests(RoslynWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec => GraphTemplates.Find("skill-proposer")!.Spec;

    [Fact]
    public void The_graph_lists_the_skills_then_matches_and_drafts_at_most_five_findings_and_it_validates_with_its_code_compiled()
    {
        var spec = Spec;

        Assert.Equal(["manual", "skill_catalog", "for_each", "prompt", "code", "condition", "run_agent", "code", "collect", "code", "notify"], spec.Nodes.Select(static n => n.Type), StringComparer.Ordinal);
        var each = spec.Nodes.Single(static n => n.Type == "for_each");
        Assert.Equal(("{{trigger.payload.findings}}", 5), (each.Config["items"]!.GetValue<string>(), each.Config["maxItems"]!.GetValue<int>()));
        Assert.Contains(spec.Edges, static e => e.Output == "true" && e.To.Value == "draft");
        Assert.Equal("{{trigger.payload.repo}}", spec.Nodes.Single(static n => n.Type == "run_agent").Config["repo"]!.GetValue<string>());
        var result = Validator().Validate(spec, GraphMode.Activate);
        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void The_draft_prompt_names_only_the_free_eval_modes_and_forbids_a_real_eval_run_in_words()
    {
        var prompt = Spec.Nodes.Single(static n => n.Type == "run_agent").Config["prompt"]!.GetValue<string>();

        Assert.Contains("--selftest", prompt, StringComparison.Ordinal);
        Assert.Contains("--lint-descriptions", prompt, StringComparison.Ordinal);
        Assert.Contains("--dry-run", prompt, StringComparison.Ordinal);
        Assert.Contains("never run skill-evals.sh without --selftest, --lint-descriptions or --dry-run", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skills/{{pick.result.skill}}/", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{item.refs}}", prompt, StringComparison.Ordinal);   // the agent sees the generic mistake, never the turns behind it
    }

    [Theory]
    [InlineData("review", "review")]
    [InlineData("Review", "none")]
    [InlineData("rootCause", "none")]
    [InlineData("none", "none")]
    [InlineData("", "none")]
    public void Pick_keeps_a_skill_only_when_the_catalog_has_that_exact_name(string answered, string kept)
    {
        var picked = Run("pick", new JsonObject { ["skills"] = new JsonObject { ["skills"] = _skills.DeepClone() }, ["match"] = new JsonObject { ["answer"] = new JsonObject { ["skill"] = answered, ["why"] = "it runs the tests" } } });

        Assert.Equal(kept, picked["skill"]!.GetValue<string>());
    }

    [Fact]
    public void Pick_turns_an_answer_without_a_skill_field_into_none()
    {
        var picked = Run("pick", new JsonObject { ["skills"] = new JsonObject { ["skills"] = _skills.DeepClone() }, ["match"] = new JsonObject { ["answer"] = new JsonObject { ["why"] = 3 } } });

        Assert.Equal("none", picked["skill"]!.GetValue<string>());
    }

    [Fact]
    public void Entry_keeps_the_skill_the_report_and_the_branch_and_drops_what_is_malformed()
    {
        var entry = Run("entry", new JsonObject
        {
            ["pick"] = new JsonObject { ["result"] = new JsonObject { ["skill"] = "review", ["why"] = "x" } },
            ["draft"] = new JsonObject
            {
                ["report"] = new JsonObject { ["title"] = "Run the tests before done", ["files"] = new JsonArray("skills/review/SKILL.md", 7), ["checks"] = "selftest passed" },
                ["branch"] = "wf/skill-proposer/1-1-draft-1",
                ["commit"] = "0123abcd",
            },
        });

        Assert.Equal("review", entry["skill"]!.GetValue<string>());
        Assert.Equal("Run the tests before done", entry["title"]!.GetValue<string>());
        Assert.Equal(["skills/review/SKILL.md"], entry["files"]!.AsArray().Select(static f => f!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(("wf/skill-proposer/1-1-draft-1", "0123abcd", "selftest passed"), (entry["branch"]!.GetValue<string>(), entry["commit"]!.GetValue<string>(), entry["checks"]!.GetValue<string>()));
    }

    [Fact]
    public void Summary_counts_the_drafts_lists_each_and_says_how_many_findings_had_no_skill()
    {
        JsonArray drafts = [Draft("review", "Run the tests before done"), Draft("syncDocs", "Update the changelog")];

        var summary = Run("summary", new JsonObject { ["gather"] = new JsonObject { ["items"] = drafts }, ["each"] = new JsonObject { ["count"] = 3 } });

        Assert.Equal(2, summary["count"]!.GetValue<int>());
        var text = summary["text"]!.GetValue<string>();
        Assert.Contains("review: Run the tests before done", text, StringComparison.Ordinal);
        Assert.Contains("1 finding had no skill", text, StringComparison.Ordinal);
    }

    private static JsonObject Skill(string name) => new() { ["name"] = name, ["description"] = $"what {name} does" };

    private static JsonObject Draft(string skill, string title) => new() { ["skill"] = skill, ["title"] = title, ["files"] = new JsonArray(), ["checks"] = "ok", ["branch"] = "wf/b", ["commit"] = "c" };

    private static GraphValidator Validator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CodeBlock(runner: null!), .. GraphBlockRegistry.SkillBlocks(new FakeSkillCatalog([]))]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []),
        codeCheck: body => [.. new CodeCompiler().Compile(body, Token).Diagnostics.Select(static d => new CodeProblem(d.Line, d.Column, d.Id, d.Message))]);

    // why: the engine interpolates the node's 'input' with the run's values, then hands the code its JSON text; the same Template.Render runs here
    private static JsonNode Run(string nodeId, JsonObject scope)
    {
        var config = Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config;
        var input = Template.Render(config["input"]!.GetValue<string>(), reference => Lookup(scope, reference));
        var result = InProcessCodePort.Execute(config["code"]!.GetValue<string>(), input?.ToJsonString() ?? "null", Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return JsonNode.Parse(result.Value)!;
    }

    private static JsonNode? Lookup(JsonObject scope, string reference)
    {
        JsonNode? node = scope;
        foreach (var part in reference.Split('.'))
        {
            node = node is JsonObject map ? map[part] : null;
        }

        return node;
    }
}
