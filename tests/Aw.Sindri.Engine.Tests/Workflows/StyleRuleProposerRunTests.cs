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
/// The 'style-rule-proposer' starter end to end on the time-skipping server (issue 3787, piece 4): the shipped spec, the real prompt, code, condition, loop and notify blocks,
/// a model that classifies findings and a scripted agent for each draft.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StyleRuleProposerRunTests
{
    private static readonly TimeSpan _long = TimeSpan.FromMinutes(3);   // why: the first Roslyn compile of the four bodies takes seconds on a cold runner

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_checkable_findings_get_one_draft_each_and_the_uncheckable_one_is_counted()
    {
        var model = new ClassifyingModel();
        var agents = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered("""{"report":{"title":"Ban string.Format","files":["rules/engineering/csharp/x.yaml"],"checks":"unbuilt","violationsOnMain":2}}""", Reason: null) };
        await using var rig = await GraphRig.StartAsync(agents, Blocks(model));
        var workflow = await rig.Define(GraphTemplates.Find("style-rule-proposer")!.Spec);
        var trigger = Trigger(
            Finding("naming", "used string.Format for text", "transcript:1111aaaa#3", "transcript:2222bbbb#4"),
            Finding("layering", "called the repository from a service", "transcript:1111aaaa#9", "transcript:3333cccc#1"),
            Finding("restate", "ignored the earlier answer", "transcript:2222bbbb#8", "transcript:3333cccc#5"));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        await agents.Watching.WaitAsync(TimeSpan.FromSeconds(60), Token);
        agents.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
        var run = await rig.Final(started.RunId, _long);
        var gather = JsonNode.Parse((await rig.ItemRuns(started.RunId, "gather"))[0].Output!)!;
        var summary = JsonNode.Parse((await rig.ItemRuns(started.RunId, "summary"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        Assert.Equal(2, agents.Launches.Count);
        Assert.Equal(["engineering.csharp.no-format-calls", "engineering.csharp.no-repo-in-services"], gather["items"]!.AsArray().Select(static i => i!["ruleId"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Contains("1 finding got no draft", summary["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["Style-rule proposer: 2 drafts"], rig.Ports.Notified);
        Assert.All(model.Prompts, static p => Assert.DoesNotContain("transcript:", p, StringComparison.Ordinal));   // the classify prompt sees the mistake, never the turns' refs
    }

    [Fact]
    public async Task A_finding_an_existing_rule_covers_gets_no_draft()
    {
        var model = new ClassifyingModel();
        var agents = new FakeAgentNodeRuntime();
        await using var rig = await GraphRig.StartAsync(agents, Blocks(model));
        var workflow = await rig.Define(GraphTemplates.Find("style-rule-proposer")!.Spec);
        var trigger = Trigger(Finding("culture", "formatted a number without the invariant culture", "transcript:1111aaaa#3", "transcript:2222bbbb#4"));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        var run = await rig.Final(started.RunId, _long);

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        Assert.Empty(agents.Launches);
        Assert.Equal(["Style-rule proposer: 0 drafts"], rig.Ports.Notified);
    }

    [Fact]
    public async Task Five_findings_run_the_first_three_and_the_run_succeeds()
    {
        var model = new ClassifyingModel();
        var agents = new FakeAgentNodeRuntime();
        await using var rig = await GraphRig.StartAsync(agents, Blocks(model));
        var workflow = await rig.Define(GraphTemplates.Find("style-rule-proposer")!.Spec);
        var findings = new List<JsonObject>();
        for (var i = 1; i <= 5; i++)
        {
            findings.Add(Finding("label" + i.ToString(CultureInfo.InvariantCulture), "an unchecked mistake, number " + i.ToString(CultureInfo.InvariantCulture), "transcript:1111aaaa#3", "transcript:2222bbbb#4"));
        }

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger([.. findings]).ToJsonString());
        var run = await rig.Final(started.RunId, _long);
        var summary = JsonNode.Parse((await rig.ItemRuns(started.RunId, "summary"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        Assert.Equal(3, model.Prompts.Count);
        Assert.Empty(agents.Launches);
        Assert.Contains("2 more findings were past the top 3 and got no draft", summary["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["Style-rule proposer: 0 drafts"], rig.Ports.Notified);
    }

    private static IReadOnlyList<IGraphBlock> Blocks(ClassifyingModel model)
    {
        var rules = new FakeRuleCatalog(
        [
            new RuleCatalogSeed("engineering.csharp.use-invariant-culture", "4b.2", "Pass CultureInfo.InvariantCulture when formatting.", [new RuleCheckListing("analyzer", "CA1305")]),
            new RuleCatalogSeed("engineering.csharp.one-type-per-file", "4b.1", "Write one type per file.", []),
        ]);
        return [new PromptBlock(model), new CodeBlock(new InProcessCodePort()), .. GraphBlockRegistry.RuleBlocks(rules)];
    }

    private static JsonObject Trigger(params JsonObject[] findings)
    {
        var list = new JsonArray();
        foreach (var finding in findings)
        {
            list.Add((JsonNode)finding);
        }

        return new JsonObject { ["kind"] = "manual", ["payload"] = new JsonObject { ["repo"] = "toolkit", ["run"] = "wfr_7", ["findings"] = list } };
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

    /// <summary>Calls string.Format an analyzer rule, the repository call an archunit rule, the culture mistake a duplicate, and anything else uncheckable.</summary>
    private sealed class ClassifyingModel : IGraphModelPort
    {
        private readonly ConcurrentQueue<string> _prompts = new();

        public IReadOnlyList<string> Prompts => [.. _prompts];

        public string Untrusted(string source, string text) => $"<untrusted source=\"{source}\">{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            _prompts.Enqueue(prompt);
            var answer = prompt.Contains("string.Format for text", StringComparison.Ordinal)
                ? Answer("analyzer", "CA1822", "no-format-calls", "4b.2", string.Empty)
                : prompt.Contains("repository from a service", StringComparison.Ordinal)
                    ? Answer("archunit", "ARCH-LAYER-009", "no-repo-in-services", "4b.1", string.Empty)
                    : prompt.Contains("invariant culture", StringComparison.Ordinal)
                        ? Answer("none", string.Empty, string.Empty, string.Empty, "engineering.csharp.use-invariant-culture")
                        : Answer("none", string.Empty, string.Empty, string.Empty, string.Empty);
            return ValueTask.FromResult<Result<string>>(new JsonObject { ["answer"] = answer }.ToJsonString());
        }

        private static JsonObject Answer(string kind, string checkId, string slug, string section, string duplicateOf) => new()
        {
            ["kind"] = kind,
            ["checkId"] = checkId,
            ["slug"] = slug,
            ["section"] = section,
            ["text"] = kind == "none" ? string.Empty : "A rule in one sentence.",
            ["why"] = kind == "none" ? string.Empty : "Because a person corrected it twice.",
            ["duplicateOf"] = duplicateOf,
        };
    }
}
