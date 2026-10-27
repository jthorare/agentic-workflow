using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A parked run says which rewind would unblock it (#1270, ask 4): the park text names 'sindri workflow rewind', and a parked instance lists
/// 'ApplyCommand:rewind' in allowedCommands, one per state it can go back to, the state of the withheld transition first. The owner's case:
/// workflow.scoping parked no-progress on evidence -> draft, which a rewind to evidence (the check, on the facts it already holds) unblocks.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RewindOffersTests
{
    private const string Address = "T-3/scoping/1";

    private static readonly string[] _walked =
    [
        "intake", "read-brief", "gather", "relevance", "relevance-judge", "relevance-apply", "draft", "check", "questions", "interview", "record", "apply",
        "challenge", "challenge-log", "size", "propose", "evidence",
    ];

    private static readonly string[] _evidenceReads =
    [
        "fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:answers", "fact:answerLog", "fact:challenge", "fact:openQuestions",
        "fact:decision", "fact:anchors", "fact:questions", "fact:relevance", "fact:gapSearch", "fact:mapFixes",
    ];

    private static readonly RecipeDocument _document = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml");

    private static Recipe Scoping => _document.Recipe;

    /// <summary>The owner's instance: evidence reported 'incomplete' twice with the same findings, so entering draft again parked it; the frame is still at evidence.</summary>
    private static WorkflowInstance ParkedOnEvidence(params string[] without)
    {
        var withheld = Scoping.Transitions.ToList().FindIndex(static t => t.From.Contains("evidence", StringComparer.Ordinal) && t.To == "draft");
        var at = At(Scoping, "evidence", entry: 17);
        return at with
        {
            Frame = at.Frame with { Run = null },
            Visited = _walked,
            Facts = _evidenceReads.Except(without, StringComparer.Ordinal).Append("fact:findings").ToDictionary(static n => FactKey.From(n), static n => Fact(n), EqualityComparer<FactKey>.Default),
            Suspension = new Suspension(
                SuspensionKind.Park, ParkReason.NoProgress, ParkKind.Loop, Escalation: null, "round 2 of rounds made no progress over round 1 (the same findings twice)", "service:engine", Mirrors: null,
                new ResumeAction(ResumeKind.Transition, new WithheldTrigger(withheld, Completed(17, "incomplete", ("fact:findings", "F"))), Gate: null, Effects: [])),
        };
    }

    private static InterpretedRecipe Interpreted() => new(new RecipeVersion(Scoping.Id, Scoping.Version, _document.Hash), Scoping, EngineFixture.Catalog, new Dictionary<FactKey, InputFact>());

    private static InstanceView View(WorkflowInstance instance) =>
        InstanceViews.Of(instance, Interpreted(), DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext(Version: 1, Children: null));

    [Fact]
    public void A_run_parked_on_evidence_to_draft_offers_evidence_first_and_never_a_state_a_rewind_refuses()
    {
        var targets = TransitionDecision.RewindTargets(Scoping, EngineFixture.Catalog, ParkedOnEvidence());

        Assert.Equal("evidence", targets[0]);
        Assert.DoesNotContain("propose", targets, StringComparer.Ordinal);   // changes the map outside the instance
        Assert.DoesNotContain("stop", targets, StringComparer.Ordinal);
        Assert.Equal(targets.Count, targets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_state_whose_Step_reads_a_fact_the_run_does_not_hold_is_not_offered()
    {
        var targets = TransitionDecision.RewindTargets(Scoping, EngineFixture.Catalog, ParkedOnEvidence(without: "fact:brief"));

        Assert.DoesNotContain("evidence", targets, StringComparer.Ordinal);   // a rewind to it is refused: 'evidence' reads fact:brief
    }

    [Fact]
    public void A_taken_over_or_ended_run_is_offered_nothing()
    {
        var parked = ParkedOnEvidence();

        Assert.Empty(TransitionDecision.RewindTargets(Scoping, EngineFixture.Catalog, parked with { Outcome = OutcomeName.From("unresolved") }));
        Assert.Empty(TransitionDecision.RewindTargets(Scoping, EngineFixture.Catalog, parked with { Suspension = parked.Suspension! with { Kind = SuspensionKind.Takeover } }));
    }

    [Fact]
    public void The_no_progress_park_text_names_the_rewind_to_evidence_with_the_instances_address()
    {
        var next = View(ParkedOnEvidence()).Next;

        Assert.Contains($"`sindri workflow rewind {Address} --to evidence`", next, StringComparison.Ordinal);
        Assert.Contains("Grant more rounds", next, StringComparison.Ordinal);
        Assert.EndsWith("or cancel.", next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_park_with_nothing_to_rewind_to_keeps_its_old_text()
    {
        var next = View(ParkedOnEvidence(without: "fact:brief") with { Visited = ["evidence"] }).Next;

        Assert.EndsWith("Grant more rounds at a terminal, or cancel.", next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_parked_instance_lists_one_rewind_per_target_with_the_withheld_state_first_and_before_takeover_and_cancel()
    {
        var rewinds = View(ParkedOnEvidence()).AllowedCommands.Where(static c => c.Action == "ApplyCommand:rewind").ToList();

        Assert.NotEmpty(rewinds);
        Assert.Equal($"sindri workflow rewind {Address} --to evidence", rewinds[0].Cli);
        Assert.All(rewinds, static c => Assert.StartsWith($"sindri workflow rewind {Address} --to ", c.Cli, StringComparison.Ordinal));
        Assert.Equal(rewinds.Count, rewinds.Select(static c => c.Cli).Distinct(StringComparer.Ordinal).Count());
        var all = View(ParkedOnEvidence()).AllowedCommands.Select(static c => c.Action).ToList();
        Assert.True(all.LastIndexOf("ApplyCommand:rewind") < all.IndexOf("ApplyCommand:takeover"));
    }

    [Fact]
    public void A_running_instance_lists_no_rewind_even_when_it_has_been_in_earlier_states()
    {
        var running = ParkedOnEvidence() with { Suspension = null };

        Assert.DoesNotContain(View(running).AllowedCommands, static c => c.Action == "ApplyCommand:rewind");
    }

    [Fact]
    public void The_detail_lists_the_rewinds_of_a_parked_instance_beside_the_recipe_verbs_and_not_the_other_engine_commands()
    {
        var view = View(ParkedOnEvidence());

        var detail = InstanceViews.Detail(view, new InstanceDetailContext("Scoping", Parent: null, new ViewCounts(0, 0, 0, Context: null), DateTimeOffset.UnixEpoch));

        Assert.Equal([.. view.AllowedCommands.Where(static c => c.Action == "ApplyCommand:rewind")], detail.AllowedCommands);
        Assert.DoesNotContain(detail.AllowedCommands, static c => string.Equals(c.Action, "ApplyCommand:cancel", StringComparison.Ordinal) || string.Equals(c.Action, "ApplyCommand:takeover", StringComparison.Ordinal));
        var json = JsonSerializer.Serialize(detail, EvalComposition.Options);
        Assert.Equal("ApplyCommand:rewind", JsonDocument.Parse(json).RootElement.GetProperty("allowedCommands")[0].GetProperty("actionId").GetString());
    }

    [Fact]
    public void The_status_of_a_parked_run_renders_a_rewind_row_and_names_rewind_in_its_commands_line()
    {
        var view = View(ParkedOnEvidence());

        var status = InstanceViews.Status(view, DateTimeOffset.UnixEpoch);

        var rewind = status.Commands.First(static c => c.Verb == StatusVerb.Rewind);
        Assert.Equal($"sindri workflow rewind {Address} --to evidence", rewind.Cli);
        Assert.Equal("ApplyCommand:rewind", rewind.ActionId.Value);
        Assert.Contains("rewind", StatusLayout.Commands(view), StringComparison.Ordinal);
    }
}
