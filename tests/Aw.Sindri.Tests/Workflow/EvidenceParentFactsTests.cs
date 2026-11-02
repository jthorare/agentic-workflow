using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The evidence parents' facts (DT-R2 plan U6): which fact each child's summary lands under, folded through the pure engine.</summary>
public sealed class EvidenceParentFactsTests
{
    private const string Start = """{"start":{"inputs":{"fact:phasePacket":{"phase":"1"},"fact:built":"https://example.test/diff/1"}}}""";

    // why: the resolver is only read once built, so the sindri/ recipes are put into it once for the class.
    private static readonly Lazy<InMemoryChildRecipes> _siblings = new(Siblings);

    private static InMemoryChildRecipes Siblings()
    {
        var children = InMemoryChildRecipes.None;
        foreach (var path in ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal)))
        {
            var recipe = EvalComposition.Recipe(path);
            _ = children.With(new ChildRecipe(recipe, recipe.Hash!.Value));
        }

        return children;
    }

    private static EvalSimulation Fold(string recipePath, params string[] lines)
    {
        var recipe = EvalComposition.Recipe(recipePath);
        var read = EvalCaseReader.Read("facts", string.Join('\n', [Start, .. lines]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var children = _siblings.Value;
        var simulation = EvalSimulation.Start(evalCase, recipe, EvalComposition.Catalog, children).Value;
        for (var i = 0; i < evalCase.Steps.Count; i++)
        {
            var step = simulation.Step(evalCase.Steps[i], i + 2);
            Assert.False(step.IsError, step.IsError ? step.Error.Message : null);
            simulation = step.Value.Next;
            Assert.Null(simulation.Diverges(evalCase.Steps[i].Expect));
        }

        return simulation;
    }

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Child(string state, string outcome, string? summary = null) =>
        JsonSerializer.Serialize(new
        {
            trigger = new { kind = "wait.satisfied", outcome, outputs = summary is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:evidenceSummary"] = summary } },
            expect = new { state },
        });

    [Fact, Trait("Lane", "unit")]
    public void Evidence_all_keeps_each_childs_summary_under_its_own_key()
    {
        var simulation = Fold("sindri/workflow.evidence-all.yaml",
            Child("api", "resolved", "ui summary"), Child("db", "resolved", "api summary"), Child("combine", "resolved", "db summary"));

        var facts = simulation.Instance.Facts;
        Assert.Equal(Digest("ui summary"), facts[FactKey.From("fact:evidenceUi")].Sha256);
        Assert.Equal(Digest("api summary"), facts[FactKey.From("fact:evidenceApi")].Sha256);
        Assert.Equal(Digest("db summary"), facts[FactKey.From("fact:evidenceDb")].Sha256);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_all_failing_at_the_api_child_leaves_no_evidenceApi_and_no_summary()
    {
        var simulation = Fold("sindri/workflow.evidence-all.yaml",
            Child("api", "resolved", "ui summary"), Child("failed", "unresolved", "api broke"));

        Assert.DoesNotContain(FactKey.From("fact:evidenceSummary"), simulation.Instance.Facts.Keys);
        Assert.DoesNotContain(FactKey.From("fact:evidenceApi"), simulation.Instance.Facts.Keys);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ui", "evidence-ui")]
    [InlineData("api", "evidence-api")]
    [InlineData("db", "evidence-db")]
    [InlineData("desktop", "evidence-desktop")]
    [InlineData("all", "evidence-all")]
    [InlineData("other", "evidence-other")]
    public void Evidence_routes_every_leaf_choice_and_reconverges_on_the_summary(string choice, string state)
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"CHOICE"},"expect":{"state":"STATE"}}""".Replace("CHOICE", choice, StringComparison.Ordinal).Replace("STATE", state, StringComparison.Ordinal),
            Child("done", "resolved", "proof"));

        Assert.Equal("resolved", simulation.Instance.Outcome?.Value);
        Assert.Equal(Digest("proof"), simulation.Instance.Facts[FactKey.From("fact:evidenceSummary")].Sha256);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_leaf_carries_its_summary_to_the_route()
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"api"},"expect":{"state":"evidence-api"}}""",
            Child("failed", "unresolved", "api failed"));

        Assert.Equal(Digest("api failed"), simulation.Instance.Facts[FactKey.From("fact:evidenceSummary")].Sha256);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_ends_all_failed_with_nothing_recorded_when_evidence_all_is_unresolved()
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"all"},"expect":{"state":"evidence-all"}}""",
            Child("all-failed", "unresolved", "partial"));

        Assert.Equal("unresolved", simulation.Instance.Outcome?.Value);
        Assert.DoesNotContain(FactKey.From("fact:evidenceSummary"), simulation.Instance.Facts.Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_drops_when_evidence_all_is_cancelled()
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"all"},"expect":{"state":"evidence-all"}}""",
            Child("dropped", "cancelled"));

        Assert.Equal("unresolved", simulation.Instance.Outcome?.Value);
        Assert.DoesNotContain(FactKey.From("fact:evidenceSummary"), simulation.Instance.Facts.Keys);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("pass", "matches", "passed", true)]
    [InlineData("fail", "header drifts", "failed", true)]
    [InlineData("skipped", null, "passed", false)]
    public void Verify_records_the_design_check_on_pass_and_fail_and_not_on_skipped(string outcome, string? content, string state, bool recorded)
    {
        var outputs = content is null ? string.Empty : ",\"outputs\":{\"fact:designCheck\":\"" + content + "\"}";
        var simulation = Fold("sindri/workflow.verify.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"pass","outputs":{"fact:evidencePack":"pack"}},"expect":{"state":"design"}}""",
            "{\"trigger\":{\"kind\":\"step.completed\",\"outcome\":\"" + outcome + "\"" + outputs + "},\"expect\":{\"state\":\"" + state + "\"}}");

        var key = FactKey.From("fact:designCheck");
        Assert.Equal(recorded, simulation.Instance.Facts.ContainsKey(key));
        if (recorded)
        {
            Assert.Equal(Digest(content!), simulation.Instance.Facts[key].Sha256);
        }
    }
}
