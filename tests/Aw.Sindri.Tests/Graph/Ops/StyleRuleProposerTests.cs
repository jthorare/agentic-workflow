using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
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
/// The 'style-rule-proposer' starter (issue 3787, piece 4): its graph, the draft prompt's rules, and its four code bodies run on the inputs the engine renders
/// from their config. Each body is the exact text the template ships, compiled by the real compiler and run in process.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StyleRuleProposerTests : IClassFixture<RoslynWarmup>
{
    /// <summary>Takes the warm-up so xUnit builds it before the first fact; the fixture's work is done in its constructor.</summary>
    /// <param name="warmup">The warm-up, already run.</param>
    public StyleRuleProposerTests(RoslynWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec => GraphTemplates.Find("style-rule-proposer")!.Spec;

    [Fact]
    public void The_graph_lists_the_rules_then_classifies_and_drafts_at_most_three_findings_and_it_validates_with_its_code_compiled()
    {
        var spec = Spec;

        Assert.Equal(["manual", "code", "rule_catalog", "code", "for_each", "prompt", "code", "condition", "run_agent", "code", "collect", "code", "notify"], spec.Nodes.Select(static n => n.Type), StringComparer.Ordinal);
        var each = spec.Nodes.Single(static n => n.Type == "for_each");
        Assert.Equal(("{{check.result.top}}", 3), (each.Config["items"]!.GetValue<string>(), each.Config["maxItems"]!.GetValue<int>()));
        Assert.Contains(spec.Edges, static e => e.Output == "true" && e.To.Value == "draft");
        var draft = spec.Nodes.Single(static n => n.Type == "run_agent");
        Assert.Equal(("{{trigger.payload.repo}}", "60m"), (draft.Config["repo"]!.GetValue<string>(), draft.Config["timeout"]!.GetValue<string>()));
        var result = Validator().Validate(spec, GraphMode.Activate);
        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void The_draft_prompt_names_check_rules_and_one_light_build_and_forbids_push_and_pull_requests_in_words()
    {
        var prompt = DraftPrompt();

        Assert.Contains("node scripts/check-rules.mjs", prompt, StringComparison.Ordinal);
        Assert.Contains("one light build", prompt, StringComparison.Ordinal);
        Assert.Contains("unbuilt", prompt, StringComparison.Ordinal);
        Assert.Contains("fix nothing outside the rule's own files", prompt, StringComparison.Ordinal);
        Assert.Contains("do not push and do not open a pull request", prompt, StringComparison.Ordinal);
        Assert.Contains("rules/engineering/csharp.inventory.yaml", prompt, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.<ID>.severity = error", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{item.refs}}", prompt, StringComparison.Ordinal);   // the agent sees the generic mistake, never the turns behind it
    }

    [Fact]
    public void The_draft_prompt_puts_no_transcript_ref_in_the_record_and_cites_the_miner_run()
    {
        var prompt = DraftPrompt();

        Assert.Contains("Never put a transcript ref or any text from a session in the record", prompt, StringComparison.Ordinal);
        Assert.Contains("source {type: correction, ref: \"{{check.result.source}}\"}", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"findings":[]}""")]
    [InlineData("""{"repo":"  ","findings":[]}""")]
    [InlineData("null")]
    public void A_payload_with_no_repo_fails_the_run_at_check_with_a_named_error(string payload)
    {
        var result = Execute("check", new JsonObject { ["trigger"] = new JsonObject { ["payload"] = JsonNode.Parse(payload) } });

        Assert.True(result.IsError);
        Assert.Contains("the payload needs 'repo'", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_payload_with_a_repo_and_no_findings_list_fails_naming_findings_and_a_full_one_passes()
    {
        var missing = Execute("check", new JsonObject { ["trigger"] = new JsonObject { ["payload"] = new JsonObject { ["repo"] = "toolkit" } } });
        var full = Execute("check", new JsonObject { ["trigger"] = new JsonObject { ["payload"] = new JsonObject { ["repo"] = "toolkit", ["findings"] = new JsonArray(1, 2), ["run"] = "wfr_9" } } });

        Assert.Contains("the payload needs 'findings'", missing.Error.Message, StringComparison.Ordinal);
        var passed = JsonNode.Parse(full.Value)!;
        Assert.Equal(2, passed["findings"]!.GetValue<int>());
        Assert.Equal("steering-miner wfr_9", passed["source"]!.GetValue<string>());
    }

    [Fact]
    public void Check_hands_on_the_first_three_findings_in_order_and_still_counts_all_of_them()
    {
        var findings = new JsonArray();
        for (var i = 1; i <= 5; i++)
        {
            findings.Add((JsonNode)new JsonObject { ["label"] = "label" + i.ToString(CultureInfo.InvariantCulture), ["mistake"] = "mistake " + i.ToString(CultureInfo.InvariantCulture) });
        }

        var check = Run("check", new JsonObject { ["trigger"] = new JsonObject { ["payload"] = new JsonObject { ["repo"] = "toolkit", ["findings"] = findings } } });

        Assert.Equal(5, check["findings"]!.GetValue<int>());
        Assert.Equal(["label1", "label2", "label3"], check["top"]!.AsArray().Select(static f => f!["label"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("steering-miner", check["source"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("analyzer", "CA1822")]
    [InlineData("archunit", "ARCH-CQRS-001")]
    [InlineData("banned-symbol", "M:System.String.Format(System.String,System.Object)")]
    public void Vet_keeps_an_analyzer_an_archunit_and_a_banned_symbol_answer(string kind, string checkId)
    {
        var vetted = Run("vet", Scope(Answer(kind, checkId)));

        Assert.Equal((kind, checkId, "no-format-calls", "4b.2", string.Empty), (vetted["kind"]!.GetValue<string>(), vetted["checkId"]!.GetValue<string>(), vetted["slug"]!.GetValue<string>(), vetted["section"]!.GetValue<string>(), vetted["reason"]!.GetValue<string>()));
        Assert.Equal("Call string.Create, never string.Format.", vetted["text"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("review")]
    [InlineData("")]
    public void Vet_turns_any_other_kind_into_none_not_checkable(string kind)
    {
        var vetted = Run("vet", Scope(Answer(kind, "CA1822")));

        Assert.Equal(("none", "not_checkable"), (vetted["kind"]!.GetValue<string>(), vetted["reason"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("analyzer", "CA182", "no-format-calls", "4b.2")]
    [InlineData("analyzer", "XX1822", "no-format-calls", "4b.2")]
    [InlineData("archunit", "ARCH-cqrs-001", "no-format-calls", "4b.2")]
    [InlineData("banned-symbol", "M:System.String.Format X", "no-format-calls", "4b.2")]
    [InlineData("banned-symbol", "", "no-format-calls", "4b.2")]
    [InlineData("analyzer", "CA1822", "No Format Calls", "4b.2")]
    [InlineData("analyzer", "CA1822", "no-format-calls-", "4b.2")]
    [InlineData("analyzer", "CA1822", "no-format-calls", "9z.9")]
    public void Vet_refuses_a_malformed_check_id_slug_or_section_as_malformed(string kind, string checkId, string slug, string section)
    {
        var answer = Answer(kind, checkId);
        answer["slug"] = slug;
        answer["section"] = section;

        var vetted = Run("vet", Scope(answer));

        Assert.Equal(("none", "malformed"), (vetted["kind"]!.GetValue<string>(), vetted["reason"]!.GetValue<string>()));
    }

    [Fact]
    public void Vet_refuses_a_check_id_an_existing_record_lists_and_a_real_duplicate_of_as_duplicate()
    {
        var listed = Run("vet", Scope(Answer("analyzer", "CA1305")));
        var named = Answer("analyzer", "CA1822");
        named["duplicateOf"] = "engineering.csharp.use-invariant-culture";
        var covered = Run("vet", Scope(named));
        var taken = Answer("analyzer", "CA1822");
        taken["slug"] = "use-invariant-culture";
        var sameSlug = Run("vet", Scope(taken));
        var invented = Answer("analyzer", "CA1822");
        invented["duplicateOf"] = "engineering.csharp.no-such-rule";
        var unknown = Run("vet", Scope(invented));

        Assert.All(new[] { listed, covered, sameSlug }, static v => Assert.Equal(("none", "duplicate"), (v["kind"]!.GetValue<string>(), v["reason"]!.GetValue<string>())));
        Assert.Equal("analyzer", unknown["kind"]!.GetValue<string>());   // a duplicateOf that names no record is the model's slip, not a duplicate
    }

    [Fact]
    public void Entry_keeps_the_rule_the_report_and_the_branch_and_drops_what_is_malformed()
    {
        var entry = Run("entry", new JsonObject
        {
            ["vet"] = new JsonObject { ["result"] = new JsonObject { ["kind"] = "analyzer", ["checkId"] = "CA1822", ["slug"] = "no-format-calls" } },
            ["draft"] = new JsonObject
            {
                ["report"] = new JsonObject { ["title"] = "Ban string.Format", ["files"] = new JsonArray("rules/engineering/csharp/no-format-calls.yaml", 7), ["checks"] = "check-rules passed; build unbuilt", ["violationsOnMain"] = 4 },
                ["branch"] = "wf/style-rule-proposer/1-1-draft-1",
                ["commit"] = "0123abcd",
            },
        });
        var malformed = Run("entry", new JsonObject
        {
            ["vet"] = new JsonObject { ["result"] = new JsonObject { ["kind"] = "analyzer", ["checkId"] = "CA1822", ["slug"] = "no-format-calls" } },
            ["draft"] = new JsonObject { ["report"] = new JsonObject { ["title"] = 5, ["violationsOnMain"] = "many" }, ["branch"] = "b", ["commit"] = "c" },
        });

        Assert.Equal(("engineering.csharp.no-format-calls", "CA1822", "Ban string.Format", 4), (entry["ruleId"]!.GetValue<string>(), entry["checkId"]!.GetValue<string>(), entry["title"]!.GetValue<string>(), entry["violationsOnMain"]!.GetValue<int>()));
        Assert.Equal(["rules/engineering/csharp/no-format-calls.yaml"], entry["files"]!.AsArray().Select(static f => f!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(("wf/style-rule-proposer/1-1-draft-1", "0123abcd", "check-rules passed; build unbuilt"), (entry["branch"]!.GetValue<string>(), entry["commit"]!.GetValue<string>(), entry["checks"]!.GetValue<string>()));
        Assert.Equal(string.Empty, malformed["title"]!.GetValue<string>());
        Assert.Null(malformed["violationsOnMain"]);
    }

    [Fact]
    public void Summary_counts_the_drafts_the_findings_with_no_draft_and_the_findings_past_the_top_three()
    {
        JsonArray drafts = [Draft("engineering.csharp.no-format-calls", "Ban string.Format")];

        var summary = Run("summary", new JsonObject { ["gather"] = new JsonObject { ["items"] = drafts }, ["each"] = new JsonObject { ["count"] = 3 }, ["check"] = new JsonObject { ["result"] = new JsonObject { ["findings"] = 5 } } });

        Assert.Equal(1, summary["count"]!.GetValue<int>());
        var text = summary["text"]!.GetValue<string>();
        Assert.Contains("engineering.csharp.no-format-calls: Ban string.Format (branch wf/b)", text, StringComparison.Ordinal);
        Assert.Contains("2 findings got no draft", text, StringComparison.Ordinal);
        Assert.Contains("2 more findings were past the top 3 and got no draft", text, StringComparison.Ordinal);
    }

    private static string DraftPrompt() => Spec.Nodes.Single(static n => n.Type == "run_agent").Config["prompt"]!.GetValue<string>();

    private static JsonObject Draft(string ruleId, string title) => new() { ["ruleId"] = ruleId, ["checkId"] = "CA1822", ["title"] = title, ["files"] = new JsonArray(), ["checks"] = "ok", ["branch"] = "wf/b", ["commit"] = "c" };

    private static JsonObject Answer(string kind, string checkId) => new()
    {
        ["kind"] = kind,
        ["checkId"] = checkId,
        ["slug"] = "no-format-calls",
        ["section"] = "4b.2",
        ["text"] = "Call string.Create, never string.Format.",
        ["why"] = "Format allocates and hides culture.",
        ["duplicateOf"] = string.Empty,
    };

    private static JsonObject Scope(JsonObject answer) => new()
    {
        ["trim"] = new JsonObject
        {
            ["result"] = new JsonObject
            {
                ["known"] = new JsonArray(
                    new JsonObject { ["id"] = "engineering.csharp.use-invariant-culture", ["checks"] = new JsonArray(new JsonObject { ["type"] = "analyzer", ["id"] = "CA1305" }) },
                    new JsonObject { ["id"] = "engineering.csharp.one-type-per-file", ["checks"] = new JsonArray() }),
            },
        },
        ["rules"] = new JsonObject { ["sections"] = new JsonArray("4b.1", "4b.2") },
        ["classify"] = new JsonObject { ["answer"] = answer },
    };

    private static GraphValidator Validator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CodeBlock(runner: null!), .. GraphBlockRegistry.RuleBlocks(new FakeRuleCatalog([]))]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []),
        codeCheck: body => [.. new CodeCompiler().Compile(body, Token).Diagnostics.Select(static d => new CodeProblem(d.Line, d.Column, d.Id, d.Message))]);

    // why: the engine interpolates the node's 'input' with the run's values, then hands the code its JSON text; the same Template.Render runs here
    private static JsonNode Run(string nodeId, JsonObject scope)
    {
        var result = Execute(nodeId, scope);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return JsonNode.Parse(result.Value)!;
    }

    private static Result<string> Execute(string nodeId, JsonObject scope)
    {
        var config = Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config;
        var input = Template.Render(config["input"]!.GetValue<string>(), reference => Lookup(scope, reference));
        return InProcessCodePort.Execute(config["code"]!.GetValue<string>(), input?.ToJsonString() ?? "null", Token);
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
