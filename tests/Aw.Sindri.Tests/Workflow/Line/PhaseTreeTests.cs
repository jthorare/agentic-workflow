using System.Text.RegularExpressions;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The recipe tree under workflow.phase (line spec §7, App. C.6; plan 2.4): who it waits on, how deep, and what it takes from each child.</summary>
public sealed partial class PhaseTreeTests
{
    private static readonly string[] _phaseChildren = ["workflow.build", "workflow.prove", "workflow.review-loop", "workflow.ship", "workflow.pr-babysitter"];

    [Fact, Trait("Lane", "unit")]
    public void Phase_waits_on_build_prove_review_ship_and_the_babysitter_and_prove_on_the_three_proof_recipes()
    {
        Assert.Equal(_phaseChildren.Order(StringComparer.Ordinal), LineTree.Children("workflow.phase").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["workflow.evidence", "workflow.hand-test", "workflow.verify"], LineTree.Children("workflow.prove").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_tree_validates_with_no_errors_and_nests_within_RCP_CHILD_003()
    {
        foreach (var id in Reachable("workflow.phase"))
        {
            Assert.Empty(LineTree.Errors(LineTree.Document(id)));
        }

        Assert.InRange(Depth("workflow.phase"), 1, 4);   // phase -> prove -> evidence -> evidence-all -> evidence-ui
    }

    [Fact, Trait("Lane", "unit")]
    public void The_tree_has_no_cycle() =>
        Assert.All(Reachable("workflow.phase"), id => Assert.DoesNotContain(id, Below(id), StringComparer.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void The_approval_order_in_the_README_lists_every_child_before_its_parent()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRecipes(), "sindri", "README.md"));
        var line = ApproveFirst.Match(readme).Value;
        Assert.NotEmpty(line);
        var tokens = Backticked.Matches(line).Select(static m => m.Groups["name"].Value).ToList();

        foreach (var id in Reachable("workflow.phase"))
        {
            var short1 = id["workflow.".Length..];
            Assert.Contains(short1, tokens, StringComparer.Ordinal);
            foreach (var child in LineTree.Children(id))
            {
                Assert.True(tokens.IndexOf(child["workflow.".Length..]) < tokens.IndexOf(short1), $"{child} is approved before {id}");
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Phase_takes_each_note_from_the_child_that_wrote_it_under_the_long_form()
    {
        var phase = LineTree.Recipe("workflow.phase");

        var renames = phase.Transitions.SelectMany(t => t.Produces.Where(static p => p.From is not null).Select(p => (Source: t.From[0], Target: t.To, Fact: p.Fact.Value, From: p.From!.Value.Value))).ToList();

        Assert.Equal(
            [
                ("babysit", "build", "fact:buildNotes", "fact:reworkNotes"),
                ("build", "prove", "fact:phasePacket", "fact:activePacket"),
                ("prove", "build", "fact:buildNotes", "fact:findings"),
                ("review", "build", "fact:buildNotes", "fact:review"),
                ("ship", "build", "fact:buildNotes", "fact:returnNotes"),
            ],
            renames.OrderBy(static r => r.Source, StringComparer.Ordinal).ThenBy(static r => r.Fact, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Build_prepare_seeds_the_amended_packet_so_the_rename_always_has_a_value()
    {
        var build = LineTree.Recipe("workflow.build");

        var prepare = Assert.Single(build.Transitions, static t => t.From.Contains("prepare", StringComparer.Ordinal));
        Assert.Contains(prepare.Produces, static p => p.Fact.Value == "fact:activePacket");   // plan Q12: RCP-WIRE-003 holds because 'prepare' writes it before any path to 'built'
    }

    [Fact, Trait("Lane", "unit")]
    public void Taking_the_proof_pack_from_an_unresolved_prove_is_refused_by_RCP_WIRE_003()
    {
        var text = ShippedRecipes.Read("sindri/workflow.phase.yaml").Replace(
            "{ label: \"proof failed: back to build\", from: prove, to: build,   on: wait.satisfied, when: { outcome: unresolved }, produces: [{ fact: fact:buildNotes, from: fact:findings }] }",
            "{ label: \"proof failed: back to build\", from: prove, to: build,   on: wait.satisfied, when: { outcome: unresolved }, produces: [{ fact: fact:buildNotes, from: fact:findings }, fact:proofPack] }",
            StringComparison.Ordinal);
        Assert.NotEqual(ShippedRecipes.Read("sindri/workflow.phase.yaml"), text, StringComparer.Ordinal);

        var errors = LineTree.Errors(RecipeComposition.ReadClean(text, "workflow.phase.yaml"));

        Assert.Contains(errors, static e => e.Contains("RCP-WIRE-003", StringComparison.Ordinal));
    }

    /// <summary>LN-WF1: the only phase transition that carries an effect, until 'effect.line.report' reports shipped and merged too, is the owner's stop: it reports the phase failed to its line, with line = fact:lineContext.</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_owners_stop_reports_the_phase_failed_to_its_line()
    {
        var withEffects = LineTree.Recipe("workflow.phase").Transitions.Where(static t => t.Effects.Count > 0).ToList();

        var stop = Assert.Single(withEffects);
        var report = Assert.Single(stop.Effects);
        Assert.Equal(("owner", "dropped", "effect.line.report", "failed", "fact:lineContext"), (stop.From.Single(), stop.To, report.Use.Value, report.With["status"].Literal!.Value.GetString(), report.With["line"].Fact!.Value.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_babysitter_gives_up_before_phase_gives_up_on_it_so_the_outcome_propagates()
    {
        var phase = LineTree.Recipe("workflow.phase").States["babysit"].Timeout!;
        var longWaits = LineTree.Recipe("workflow.pr-babysitter").States.Where(s => s.Value.Timeout is not null).ToList();

        Assert.Equal("escalate", phase.Then.ToString(), ignoreCase: true);
        Assert.All(longWaits, s => Assert.True(s.Value.Timeout!.After.Span < phase.After.Span, $"{s.Key}: {s.Value.Timeout.After.Value} must be shorter than phase {phase.After.Value}"));
    }

    private static string RepoRecipes()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "recipes");
            if (File.Exists(Path.Combine(candidate, "sindri", "README.md")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("recipes/sindri/README.md not found above the test binaries");
    }

    private static int Depth(string id) => LineTree.Children(id).Select(static c => 1 + Depth(c)).DefaultIfEmpty(0).Max();

    private static IReadOnlyList<string> Below(string id) =>
        [.. LineTree.Children(id).SelectMany(c => Below(c).Prepend(c))];

    private static IReadOnlyList<string> Reachable(string id) => [.. Below(id).Append(id).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"Approve leaf recipes first[^\n]*(?:\n(?!\n)[^\n]*)*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApproveFirst { get; }

    [GeneratedRegex(@"`(?<name>[a-z0-9-]+)`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Backticked { get; }
}
