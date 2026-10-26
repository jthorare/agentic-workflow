using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1114: SCP-COV-003 asks only for the code-lens nodes really near an anchor (the anchors' own implementers and the nodes one edge away),
/// runs at the draft check as well as at evidence, accepts a glob in excluded[], and an answer applied through the record Step carries recordedAs.
/// </summary>
public sealed class ScopeCoverageDepthTests
{
    private const string Anchor = "surface:route/links-create";
    private const string Cov3 = "SCP-COV-003";

    private static readonly string[] _nearPaths = ["src/gen/f0.ts", "src/gen/f1.ts", "src/gen/f2.ts"];
    private static readonly string[] _nearKeys = ["code:src/gen/f0.ts", "code:src/gen/f1.ts", "code:src/gen/f2.ts"];

    private static async Task<ScopingCheckResponse> Check(ScopingFacts facts, ScopeStage stage) =>
        (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, stage), TestContext.Current.CancellationToken)).Value;

    /// <summary>The gold facts with "A1" stripped from every citations array of the acceptance items, map surfaces and open questions, so an emptied recordedAs really is recorded nowhere (#1205: a citing item records an answer).</summary>
    private static ScopingFacts WithA1RecordedNowhere()
    {
        var facts = ScopingFacts.Expiry().Edit("answerLog", n => n[0]!["recordedAs"] = new JsonArray());
        _ = facts.Edit("acceptance", n => StripA1(n["items"]!.AsArray()));
        _ = facts.Edit("scopeMap", n => StripA1(n["surfaces"]!.AsArray()));
        _ = facts.Edit("openQuestions", n => StripA1(n.AsArray()));
        return facts;
    }

    private static void StripA1(JsonArray nodes)
    {
        foreach (var citations in nodes.Select(static x => x?["citations"]).OfType<JsonArray>())
        {
            foreach (var a1 in citations.Where(static c => string.Equals(c?.GetValue<string>(), "A1", StringComparison.Ordinal)).ToList())
            {
                _ = citations.Remove(a1);
            }
        }
    }

    private static JsonObject FileNode(string path, int n) => new()
    {
        ["key"] = "code:" + path,
        ["kind"] = "file",
        ["label"] = path,
        ["band"] = "code",
        ["lensItem"] = "L-code-" + (1000 + n).ToString(CultureInfo.InvariantCulture),
    };

    private static JsonObject Implements(string path) => new()
    {
        ["from"] = "code:" + path,
        ["to"] = Anchor,
        ["type"] = "implements",
        ["provenance"] = "deterministic",
        ["confirmed"] = true,
    };

    [Fact, Trait("Lane", "unit")]
    public async Task A_thousand_code_nodes_at_depth_0_fail_COV_003_only_for_the_anchors_implementers()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n =>
        {
            var code = n["code"]!;
            for (var i = 0; i < 1000; i++)
            {
                code["nodes"]!.AsArray().Add(FileNode($"src/gen/f{i.ToString(CultureInfo.InvariantCulture)}.ts", i));
            }

            foreach (var near in _nearPaths)
            {
                code["edges"]!.AsArray().Add(Implements(near));
            }
        });

        var response = await Check(facts, ScopeStage.Evidence);

        var failed = response.Findings.Where(static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal)).Select(static f => f.Item).OfType<string>().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(_nearKeys, failed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task COV_003_reports_at_the_draft_stage()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n =>
        {
            n["code"]!["nodes"]!.AsArray().Add(FileNode("src/new/near.ts", 0));
            n["code"]!["edges"]!.AsArray().Add(Implements("src/new/near.ts"));
        });

        var response = await Check(facts, ScopeStage.Draft);

        Assert.Equal(ScopeRuleStatus.Fail, response.Rules.Single(r => string.Equals(r.Rule, Cov3, StringComparison.Ordinal)).Status);
        Assert.Contains(response.Findings, static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal) && string.Equals(f.Item, "code:src/new/near.ts", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_glob_exclusion_covers_the_files_it_matches()
    {
        var facts = ScopingFacts.Expiry()
            .Edit("lenses", n =>
            {
                for (var i = 0; i < 5; i++)
                {
                    var path = $"src/gen/g{i.ToString(CultureInfo.InvariantCulture)}.ts";
                    n["code"]!["nodes"]!.AsArray().Add(FileNode(path, i));
                    n["code"]!["edges"]!.AsArray().Add(Implements(path));
                }
            })
            .Edit("scopeMap", n => n["excluded"]!.AsArray().Add(new JsonObject { ["glob"] = "src/gen/**", ["reason"] = "generated clients, no behaviour of their own" }));

        var response = await Check(facts, ScopeStage.Evidence);

        Assert.DoesNotContain(response.Findings, static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_applied_through_record_passes_ACC_003()
    {
        var batch = new JsonObject
        {
            ["batch"] = 1,
            ["round"] = 1,
            ["actor"] = "owner",
            ["at"] = "2026-10-16T09:10:00Z",
            ["questionIds"] = new JsonArray("Q1", "Q2"),
            ["supersedes"] = new JsonArray(),
            ["answers"] = new JsonArray(new JsonObject { ["question"] = "Q1", ["option"] = "Q1.a", ["text"] = "Add a choice" }),
        };
        var recordFacts = new ScopingFacts { ["answers"] = new JsonArray(batch).ToJsonString(), ["answerLog"] = "[]" };
        var recorded = (await new ScopingDeskService().Handle(new ScopingDeskRequest("record", recordFacts), TestContext.Current.CancellationToken)).Value;
        var facts = ScopingFacts.Expiry();
        facts["answerLog"] = recorded.Outputs["answerLog"];

        var response = await Check(facts, ScopeStage.Evidence);

        Assert.DoesNotContain(response.Findings, static f => string.Equals(f.Rule, "SCP-ACC-003", StringComparison.Ordinal));
        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => string.Equals(r.Rule, "SCP-ACC-003", StringComparison.Ordinal)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ACC_003_names_the_question_of_an_answer_recorded_nowhere()
    {
        var facts = WithA1RecordedNowhere();

        var response = await Check(facts, ScopeStage.Evidence);

        var finding = Assert.Single(response.Findings, static f => string.Equals(f.Rule, "SCP-ACC-003", StringComparison.Ordinal));
        Assert.Contains("Q1", finding.Message, StringComparison.Ordinal);
    }
}
