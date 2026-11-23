using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The 'skill-proposer' starter end to end on the time-skipping server (issue 3787, piece 4): the shipped spec, the real prompt, code, condition, loop and notify blocks,
/// a model that matches two of three findings to a skill, and a scripted agent for each draft. Two drafts come back; the third finding is counted as having no skill.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SkillProposerRunTests
{
    private static readonly TimeSpan _long = TimeSpan.FromMinutes(3);   // why: the first Roslyn compile of the three bodies takes seconds on a cold runner

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_matched_findings_get_one_draft_each_and_the_unmatched_one_is_counted()
    {
        var model = new MatchingModel();
        var agents = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered("""{"report":{"title":"Say done only after the tests ran","files":["skills/review/SKILL.md"],"checks":"selftest passed"}}""", Reason: null) };
        var catalog = new FakeSkillCatalog([new SkillCatalogSeed("review", "Looks a pull request over."), new SkillCatalogSeed("bugHunt", "Fix-and-verify loop.")]);
        await using var rig = await GraphRig.StartAsync(agents, [new PromptBlock(model), new CodeBlock(new InProcessCodePort()), .. GraphBlockRegistry.SkillBlocks(catalog)]);
        var workflow = await rig.Define(GraphTemplates.Find("skill-proposer")!.Spec);
        var trigger = new JsonObject
        {
            ["kind"] = "manual",
            ["payload"] = new JsonObject
            {
                ["repo"] = "web",
                ["findings"] = new JsonArray(
                    Finding("wrong_approach_process", "claimed done without running the tests", "transcript:1111aaaa#3", "transcript:2222bbbb#4"),
                    Finding("scope_surface", "missed the settings screen", "transcript:1111aaaa#9", "transcript:3333cccc#1"),
                    Finding("restate", "ignored the earlier answer", "transcript:2222bbbb#8", "transcript:3333cccc#5")),
            },
        };

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        await agents.Watching.WaitAsync(TimeSpan.FromSeconds(60), Token);
        agents.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
        var run = await rig.Final(started.RunId, _long);
        var gather = JsonNode.Parse((await rig.ItemRuns(started.RunId, "gather"))[0].Output!)!;
        var summary = JsonNode.Parse((await rig.ItemRuns(started.RunId, "summary"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        Assert.Equal(2, agents.Launches.Count);
        Assert.Equal(["review", "bugHunt"], gather["items"]!.AsArray().Select(static i => i!["skill"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Contains("1 finding had no skill", summary["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["Skill proposer: 2 drafts"], rig.Ports.Notified);
        Assert.All(model.Prompts, static p => Assert.DoesNotContain("transcript:", p, StringComparison.Ordinal));   // the match prompt sees the mistake, never the turns' refs
    }

    [Fact]
    public async Task Seven_findings_run_the_first_five_and_the_run_succeeds()
    {
        var model = new MatchingModel();
        var agents = new FakeAgentNodeRuntime();
        var catalog = new FakeSkillCatalog([new SkillCatalogSeed("review", "Looks a pull request over.")]);
        await using var rig = await GraphRig.StartAsync(agents, [new PromptBlock(model), new CodeBlock(new InProcessCodePort()), .. GraphBlockRegistry.SkillBlocks(catalog)]);
        var workflow = await rig.Define(GraphTemplates.Find("skill-proposer")!.Spec);
        var findings = new JsonArray();
        for (var i = 1; i <= 7; i++)
        {
            findings.Add((JsonNode)Finding("label" + i.ToString(CultureInfo.InvariantCulture), "an unmatched mistake, number " + i.ToString(CultureInfo.InvariantCulture), "transcript:1111aaaa#3", "transcript:2222bbbb#4"));
        }

        var trigger = new JsonObject { ["kind"] = "manual", ["payload"] = new JsonObject { ["repo"] = "web", ["findings"] = findings } };

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        var run = await rig.Final(started.RunId, _long);
        var summary = JsonNode.Parse((await rig.ItemRuns(started.RunId, "summary"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        Assert.Equal(5, model.Prompts.Count);
        Assert.Empty(agents.Launches);
        Assert.Contains("2 more findings were past the top 5 and got no draft", summary["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["Skill proposer: 0 drafts"], rig.Ports.Notified);
    }

    private static JsonObject Finding(string label, string mistake, params string[] refs)
    {
        var list = new JsonArray();
        foreach (var reference in refs)
        {
            list.Add((JsonNode)reference);
        }

        return new JsonObject { ["label"] = label, ["mistake"] = mistake, ["count"] = refs.Length, ["sessions"] = 2, ["refs"] = list };
    }

    /// <summary>Matches 'tests' to review, 'settings' to bugHunt, and anything else to none.</summary>
    private sealed class MatchingModel : IGraphModelPort
    {
        private readonly ConcurrentQueue<string> _prompts = new();

        public IReadOnlyList<string> Prompts => [.. _prompts];

        public string Untrusted(string source, string text) => $"<untrusted source=\"{source}\">{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            _prompts.Enqueue(prompt);
            var skill = prompt.Contains("running the tests", StringComparison.Ordinal) ? "review" : prompt.Contains("settings screen", StringComparison.Ordinal) ? "bugHunt" : "none";
            return ValueTask.FromResult<Result<string>>(new JsonObject { ["answer"] = new JsonObject { ["skill"] = skill, ["why"] = "it fits" } }.ToJsonString());
        }
    }
}
