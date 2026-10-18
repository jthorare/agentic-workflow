using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Tests.Workflow.Engine;
using FixtureStatusText = Aw.Sindri.Testing.Views.StatusText;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>What 'GetInstance' renders into the view (engine spec §3.8, §6.4): detail, 'next', answer lines, commands, budget.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewsTests
{
    [Fact]
    public void A_desk_task_fills_the_recipe_next_and_names_the_complete_line_with_its_required_output()
    {
        var view = StatusCases.View("desk-task");

        Assert.Equal(("produce", "desk task"), (view.State, view.Detail));
        Assert.Equal(
            "Produce the Step output for T-3 (round 1/3), then run:\nsindri workflow complete T-3/step/1 --run 1.1 --outcome done --output fact:output=<file>",
            view.Next);
        Assert.Equal("complete, takeover, cancel", StatusLayout.Commands(view));
        Assert.Equal((0, 200_000, 0, 400_000), (view.Budget!.State, view.Budget.StateCap, view.Budget.Instance, view.Budget.InstanceCap));
        Assert.Null(view.Answer);
    }

    [Theory]
    [InlineData("parked-rounds", "rounds", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal)")]
    [InlineData("parked-no-progress-child", "no-progress (child)", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal; forwarded to the child)")]
    [InlineData("parked-budget", "budget", "sindri workflow answer T-3/step/1 --grant-tokens 100k     (at a terminal; once per park)")]
    [InlineData("parked-needs-answer-step", "needs-answer", "sindri workflow answer T-3/step/1 --text <answer>")]
    [InlineData("parked-needs-answer-wait", "needs-answer", "sindri workflow answer T-3/step/1 --resume [--text <answer>]")]
    [InlineData("parked-timeout", "timeout", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("parked-no-runtime", "SND-WF-010", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("escalated-gate", "gate", "sindri workflow answer T-3/step/1 --accept     (at a terminal)")]
    public void Each_park_reason_has_its_detail_and_answer_line(string name, string detail, string answer)
    {
        var view = StatusCases.View(name);

        Assert.Equal((detail, answer), (view.Detail, view.Answer));
        Assert.StartsWith("answer", StatusLayout.Commands(view), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parked-revoked", "spec-conflict (revoked)", "cancel", "cancel")]
    [InlineData("parked-drift", "spec-conflict (drift)", "approve", "takeover, cancel")]
    public void A_spec_conflict_park_takes_no_answer_and_names_its_way_out(string name, string detail, string way, string commands)
    {
        var view = StatusCases.View(name);

        Assert.Equal(detail, view.Detail);
        Assert.Equal(commands, StatusLayout.Commands(view));   // spec 6.4: a revoked park offers only cancel (PR 232 proof F4)
        Assert.Contains(way, view.Answer, StringComparison.Ordinal);
    }

    /// <summary>PR 232 proof F2: a dispatch park's reason already ends in its own fix, so the status says it once and adds the allowlist sentence only for SND-WF-011.</summary>
    [Theory]
    [InlineData("parked-dispatch-no-runtime", false)]
    [InlineData("parked-dispatch-effect-failed", false)]
    [InlineData("parked-dispatch-allowlist", true)]
    public void A_dispatch_park_says_its_reason_and_its_fix_once(string name, bool allowlist)
    {
        var view = StatusCases.View(name);
        var next = view.Next;

        Assert.Contains(allowlist ? "once the profile allows the effect" : "once the cause is fixed", view.Answer, StringComparison.Ordinal);   // rc2-1: the answer line follows the refusal like Next does
        Assert.Equal(1, next.Split("resume to send it again", StringSplitOptions.None).Length - 1);
        Assert.Equal(allowlist ? 1 : 0, next.Split("isn't on the profile's effect allowlist", StringSplitOptions.None).Length - 1);
        Assert.StartsWith("effect.claim.ref ", next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drift_behind_a_park_names_the_block_and_the_reapproval_first()
    {
        var view = StatusCases.View("parked-rounds-with-drift");

        Assert.Equal(
            "step.produce changed since workflow.step@" + Hash12() + " was approved; answer after the re-approval: sindri recipe approve workflow.step@" + Hash12(),
            view.Drift);
    }

    [Fact]
    public void A_drift_park_that_the_reapproval_already_ended_is_no_drift_note_and_no_drift_line()
    {
        var view = StatusCases.View("parked-rounds-with-approved-drift");

        Assert.Null(view.Drift);
        Assert.Null(view.DriftNote);
    }

    [Fact]
    public void A_drift_park_queued_behind_another_park_gives_the_same_drift_line_and_note()
    {
        var view = StatusCases.View("parked-rounds-with-later-drift");

        Assert.NotNull(view.Drift);
        Assert.Equal(("step.produce", true), (view.DriftNote!.Block.Value, view.DriftNote.NeedsReapproval));
    }

    [Fact]
    public void An_ended_instance_has_its_outcome_as_detail_and_nothing_else()
    {
        var view = StatusCases.View("done");

        Assert.Equal(("done", "resolved", string.Empty), (view.State, view.Detail, view.Next));
        Assert.Empty(view.AllowedCommands);
        Assert.Null(view.Budget);
        Assert.Null(view.Answer);
    }

    private const string Answering = "sindri workflow answer T-3/step/1 ";

    [Theory]
    [InlineData("parked-rounds", "answer-grant=--grant 1")]
    [InlineData("parked-no-progress-child", "answer-grant=--grant 1", "answer-text=--text <answer>")]
    [InlineData("parked-budget", "answer-grant-tokens=--grant-tokens 100k")]
    [InlineData("parked-budget-odd", "answer-grant-tokens=--grant-tokens 12500")]
    [InlineData("parked-mirrored-rounds", "answer-grant=--grant 1")]
    [InlineData("parked-mirrored-timeout", "answer-resume=--resume")]
    [InlineData("parked-mirrored-no-runtime", "answer-resume=--resume")]
    [InlineData("parked-mirrored-budget")]
    [InlineData("parked-budget-no-cap")]
    [InlineData("parked-mirrored-spec-conflict")]
    [InlineData("parked-mirrored-needs-answer-wait")]
    [InlineData("parked-needs-answer-step", "answer-text=--text <answer>")]
    [InlineData("parked-needs-answer-wait", "answer-resume=--resume")]
    [InlineData("parked-timeout", "answer-resume=--resume")]
    [InlineData("parked-no-runtime", "answer-resume=--resume")]
    [InlineData("escalated-gate", "answer-accept=--accept", "answer-reject=--reject")]
    [InlineData("escalated-child")]
    [InlineData("parked-mirrored-needs-answer")]
    public void A_parked_instance_serves_one_command_per_fitting_answer_variant_with_its_exact_runnable_cli(string name, params string[] expected)
    {
        var answers = AnswerEntries(StatusCases.View(name));

        Assert.Equal(expected, answers.Select(a => $"{a.Action["ApplyCommand:".Length..]}={a.Cli[Answering.Length..]}").ToArray());
        Assert.All(answers, a => Assert.StartsWith(Answering, a.Cli, StringComparison.Ordinal));
        Assert.DoesNotContain(answers, a => string.Equals(a.Action, "ApplyCommand:answer", StringComparison.Ordinal));
    }

    [Fact]
    public void The_first_served_answer_is_the_status_answers_variant()
    {
        foreach (var name in StatusCases.Names)
        {
            var view = StatusCases.View(name);
            if (AnswerEntries(view) is [var first, ..])
            {
                Assert.Equal(view.AnswerLine!.ActionId.Value, first.Action);
            }
        }
    }

    [Fact]
    public async Task Every_served_answer_cli_is_runnable_parses_and_fits_the_suspension_or_the_child()
    {
        var served = 0;
        foreach (var name in StatusCases.Names)
        {
            var recipe = StatusCases.RecipeOf(name);
            foreach (var entry in AnswerEntries(StatusCases.View(name)))
            {
                served++;
                var args = entry.Cli[Answering.Length..].Split(' ');
                Assert.DoesNotContain("[", entry.Cli, StringComparison.Ordinal);
                if (!entry.Action.EndsWith("answer-text", StringComparison.Ordinal))
                {
                    Assert.DoesNotContain("<", entry.Cli, StringComparison.Ordinal);   // only the text a person types stays a template
                }

                var parsed = await Host.Commands.ApplyCommandVerbs.AnswerOptions(args, TestContext.Current.CancellationToken);
                Assert.False(parsed.IsError, $"{name}: {entry.Cli}: {(parsed.IsError ? parsed.Error.Message : string.Empty)}");
                var answer = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), parsed.Value.Variant, parsed.Value.Text, parsed.Value.Amount, Forwarded: null);
                foreach (var target in FitTargets(name))
                {
                    var fits = AnswerFit.Check(target, recipe, answer);
                    Assert.False(fits.IsError, $"{name}: {entry.Cli} is refused by {target.Suspension!.ParkKind}: {(fits.IsError ? fits.Error.Message : string.Empty)}");
                }
            }
        }

        Assert.True(served >= 12);
    }

    /// <summary>The instance whose answer check applies: the case itself, or for a mirror the child it forwards to, with each park kind the child can have for that reason.</summary>
    private static IEnumerable<WorkflowInstance> FitTargets(string name)
    {
        var instance = StatusCases.Instance(name);
        if (instance.Suspension is not { Mirrors: not null } mirrored)
        {
            yield return instance;
            yield break;
        }

        ParkKind?[] kinds = mirrored.Reason is ParkReason.Rounds or ParkReason.NoProgress ? [ParkKind.Loop, ParkKind.Refusal] : [null, ParkKind.Step, ParkKind.Wait];
        foreach (var kind in kinds)
        {
            yield return instance with { Suspension = mirrored with { Mirrors = null, ParkKind = kind } };
        }
    }

    [Fact]
    public void Only_the_reasons_whose_fit_does_not_depend_on_the_child_serve_a_mirror_an_entry()
    {
        var fixes = Enum.GetValues<ParkReason>().Where(r => AnswerVariants.ReasonFixesChildFit(r)).ToArray();

        Assert.Equal([ParkReason.Rounds, ParkReason.NoProgress, ParkReason.Timeout, ParkReason.NoRuntime, ParkReason.Launch], fixes);
        foreach (var reason in fixes)
        {
            var fits = new ParkKind?[] { null, ParkKind.Loop, ParkKind.Refusal, ParkKind.Step, ParkKind.Wait }.Select(k => AnswerVariants.Fitting(FrameFixture.Park(reason, k, ResumeKind.Transition)).ToArray()).ToList();
            Assert.All(fits, f => Assert.Equal(fits[0], f));   // whatever the child's park kind, the same variants fit
        }
    }

    [Theory]
    [InlineData("parked-mirrored-needs-answer")]
    [InlineData("parked-mirrored-budget")]
    [InlineData("escalated-child")]
    public void A_mirror_whose_child_decides_points_its_status_line_at_the_child(string name)
    {
        var answer = StatusCases.View(name).AnswerLine!;

        Assert.Equal(Answering + "<the child's answer>", answer.Cli);
        Assert.Equal("ApplyCommand:answer-text", answer.ActionId.Value);
        Assert.Contains("(forwarded to T-3/step/1/step.4)", InstanceViews.Status(StatusCases.View(name), StatusCases.Now).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("escalated-child", true)]
    [InlineData("parked-mirrored-needs-answer", true)]
    [InlineData("parked-mirrored-needs-answer-wait", true)]
    [InlineData("parked-mirrored-budget", true)]
    [InlineData("parked-mirrored-spec-conflict", true)]
    [InlineData("parked-mirrored-two-children", true)]
    [InlineData("parked-mirrored-unlisted-child", true)]
    [InlineData("parked-no-progress-child", false)]
    [InlineData("parked-mirrored-rounds", false)]
    [InlineData("parked-mirrored-timeout", false)]
    [InlineData("parked-rounds", false)]
    [InlineData("parked-budget-no-cap", false)]
    [InlineData("escalated-gate", false)]
    public void Only_a_child_pointer_is_marked_via_child_and_names_the_child(string name, bool pointsAtChild)
    {
        var answer = StatusCases.View(name).AnswerLine!;

        Assert.Equal(pointsAtChild ? "child" : null, answer.Via);
        Assert.Equal(pointsAtChild && name != "parked-mirrored-unlisted-child" ? "T-3/step/1/step.4" : null, answer.Child);
        Assert.Equal(pointsAtChild, answer.Cli.EndsWith("<the child's answer>", StringComparison.Ordinal));
    }

    [Fact]
    public void The_child_named_is_the_one_the_suspension_mirrors_not_the_first_listed_sibling()
    {
        var view = StatusCases.View("parked-mirrored-two-children");

        Assert.Equal("T-3/step/1/step.4", view.AnswerLine!.Child);
        Assert.Contains("(forwarded to T-3/step/1/step.4)", InstanceViews.Status(view, StatusCases.Now).Text, StringComparison.Ordinal);
        Assert.DoesNotContain("step.2", InstanceViews.Status(view, StatusCases.Now).Text.Split('\n').Single(l => l.TrimStart().StartsWith("answer", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_mirrored_child_that_is_not_listed_is_not_guessed_from_a_sibling()
    {
        var view = StatusCases.View("parked-mirrored-unlisted-child");

        Assert.Null(view.AnswerLine!.Child);
        Assert.Equal("child", view.AnswerLine.Via);
        var answerLine = InstanceViews.Status(view, StatusCases.Now).Text.Split('\n').Single(l => l.TrimStart().StartsWith("answer", StringComparison.Ordinal));
        Assert.DoesNotContain("step.2", answerLine, StringComparison.Ordinal);
        Assert.Contains("(forwarded to the child)", answerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fixture_renderer_and_the_engine_print_the_same_answer_line_for_a_pointer_with_no_known_child()
    {
        var engine = AnswerLine(InstanceViews.Status(StatusCases.View("parked-mirrored-unlisted-child"), StatusCases.Now).Text);
        var fixture = AnswerLine(FixtureStatusText.Render(WorkflowViewFixtures.StatusMirroredBudgetNoChild(), WorkflowViewFixtures.Now));

        Assert.Equal(engine[engine.IndexOf("<the child's answer>", StringComparison.Ordinal)..], fixture[fixture.IndexOf("<the child's answer>", StringComparison.Ordinal)..]);
    }

    private static string AnswerLine(string text) => text.Split('\n').Single(l => l.TrimStart().StartsWith("answer", StringComparison.Ordinal)).TrimEnd();

    [Theory]
    [InlineData("parked-mirrored-needs-answer-wait")]
    [InlineData("parked-mirrored-spec-conflict")]
    [InlineData("parked-mirrored-needs-answer")]
    public void The_next_line_of_a_mirrored_park_names_the_child(string name)
        => Assert.Contains(" in T-3/step/1/step.4", StatusCases.View(name).Next, StringComparison.Ordinal);

    [Fact]
    public void The_status_answer_is_the_first_variant_the_one_table_lists_for_every_park_shape()
    {
        foreach (var reason in Enum.GetValues<ParkReason>())
        {
            foreach (var kind in (ParkKind?[])[null, .. Enum.GetValues<ParkKind>().Cast<ParkKind?>()])
            {
                AssertFirstVariant(FrameFixture.Park(reason, kind, ResumeKind.Transition));
            }
        }

        foreach (var escalation in Enum.GetValues<EscalationKind>().Where(static e => e != EscalationKind.Child))
        {
            AssertFirstVariant(FrameFixture.Park(ParkReason.Rounds, kind: null, ResumeKind.Transition) with { Kind = SuspensionKind.Escalation, Reason = null, Escalation = escalation });
        }
    }

    private static void AssertFirstVariant(Suspension shape)
    {
        var address = FrameFixture.Producing().Address;
        var fitting = AnswerVariants.Fitting(shape);
        var line = ParkAnswers.For(new ParkShape(shape.Kind, shape.Reason, shape.ParkKind, shape.Escalation), address);
        if (fitting.Count > 0)
        {
            Assert.Equal(AnswerRow.For(fitting[0]).Action, line!.ActionId.Value);
        }
        else if (shape.Kind == SuspensionKind.Escalation && shape.Escalation == EscalationKind.Child)
        {
            Assert.Equal("child", line!.Via);   // a child's escalation is only ever a pointer
        }
        else if (shape is { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked or ParkKind.ChildRevoked or ParkKind.Drift })
        {
            Assert.True(line is null || line.Kind == AnswerKind.CancelOnly, "a revoked or drifted park takes no answer");
        }
        else
        {
            // The shapes the engine never builds without a fit, skipped by name: a spec-conflict park of kind loop, refusal, step, wait or
            // dispatch (or none), and a null-reason park. Every other shape must take no answer, or only the cancel one.
            var neverBuilt = shape is { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict or null };
            if (!neverBuilt)
            {
                Assert.True(line is null || line.Kind == AnswerKind.CancelOnly, $"{shape.Kind}/{shape.Reason}/{shape.ParkKind}");
            }
        }
    }

    [Theory]
    [InlineData("escalated-child")]
    [InlineData("parked-mirrored-needs-answer")]
    public void A_mirrored_suspension_whose_childs_fit_is_not_known_serves_no_answer_command(string name)
        => Assert.Empty(AnswerEntries(StatusCases.View(name)));

    private static List<AllowedCommand> AnswerEntries(InstanceView view)
        => [.. view.AllowedCommands.Where(c => c.Action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal))];

    [Theory]
    [InlineData("parked-revoked")]
    [InlineData("parked-drift")]
    [InlineData("taken-over")]
    [InlineData("desk-task")]
    public void A_state_that_takes_no_answer_serves_no_answer_command(string name)
        => Assert.DoesNotContain(StatusCases.View(name).AllowedCommands, c => c.Action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal));

    [Theory]
    [InlineData(SuspensionKind.Park, ParkReason.Rounds, null, null, "grant")]
    [InlineData(SuspensionKind.Park, ParkReason.NoProgress, null, null, "grant,text")]
    [InlineData(SuspensionKind.Park, ParkReason.Budget, null, null, "grant-tokens")]
    [InlineData(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Wait, null, "resume")]
    [InlineData(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, null, "text")]
    [InlineData(SuspensionKind.Park, ParkReason.Timeout, null, null, "resume")]
    [InlineData(SuspensionKind.Park, ParkReason.NoRuntime, null, null, "resume")]
    [InlineData(SuspensionKind.Park, ParkReason.SpecConflict, ParkKind.Dispatch, null, "resume")]
    [InlineData(SuspensionKind.Park, ParkReason.SpecConflict, ParkKind.Drift, null, "")]
    [InlineData(SuspensionKind.Escalation, null, null, EscalationKind.Gate, "accept,reject")]
    [InlineData(SuspensionKind.Escalation, null, null, EscalationKind.Timeout, "resume")]
    [InlineData(SuspensionKind.Escalation, null, null, EscalationKind.Child, "")]
    public void The_one_variant_table_lists_what_fits_each_suspension_shape(SuspensionKind kind, ParkReason? reason, ParkKind? parkKind, EscalationKind? escalation, string expected)
    {
        var suspension = FrameFixture.Park(ParkReason.Rounds, parkKind, ResumeKind.Transition) with { Kind = kind, Reason = reason, Escalation = escalation };

        var variants = AnswerVariants.Fitting(suspension).Select(v => v.ToString().ToLowerInvariant().Replace("granttokens", "grant-tokens", StringComparison.Ordinal));

        Assert.Equal(expected, string.Join(',', variants));
    }

    [Fact]
    public void Each_answer_variant_has_one_row_pinned_to_the_catalog()
    {
        var catalog = Host.Catalog.EngineRows.All.ToDictionary(static r => r.Action.Value, StringComparer.Ordinal);
        foreach (var variant in Enum.GetValues<AnswerVariant>())
        {
            var row = AnswerRow.For(variant, "100k");
            Assert.Equal(row.Id, AnswerRow.ForAction("ApplyCommand:" + row.Id).Id);
            var entry = catalog["ApplyCommand:" + row.Id];
            Assert.Equal(row.Class, entry.Class);
            Assert.Equal(row.Option.Split(' ')[0], entry.Cli.Text.Split(' ')[4]);   // the option token after <address>, exactly
        }

        _ = Assert.Throws<System.Diagnostics.UnreachableException>(() => AnswerRow.For((AnswerVariant)99));
    }

    [Fact]
    public void The_commands_line_names_each_verb_once_as_spec_6_4_lists_them()
    {
        Assert.Contains("  commands  answer, takeover, cancel", InstanceViews.Status(StatusCases.View("escalated-gate"), StatusCases.Now).Text, StringComparison.Ordinal);
        Assert.Contains("  commands  answer (forwarded), takeover, cancel", InstanceViews.Status(StatusCases.View("parked-no-progress-child"), StatusCases.Now).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parked-rounds")]
    [InlineData("parked-budget")]
    [InlineData("parked-needs-answer-step")]
    [InlineData("parked-needs-answer-wait")]
    [InlineData("parked-timeout")]
    [InlineData("escalated-gate")]
    public void The_status_commands_carry_each_answer_variants_full_cli_equal_to_the_served_list(string name)
    {
        var view = StatusCases.View(name);
        var status = InstanceViews.Status(view, StatusCases.Now);

        var answers = status.Commands.Where(c => c.Verb == StatusVerb.Answer).ToList();

        Assert.Equal(
            [.. view.AllowedCommands.Where(c => c.Action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal)).Select(c => (c.Action, c.Cli))],
            [.. answers.Select(a => (a.ActionId.Value, a.Cli))]);
        Assert.Equal(status.Answer!.ActionId.Value, answers[0].ActionId.Value, StringComparer.Ordinal);
        Assert.All(answers, a => Assert.StartsWith(Answering, a.Cli, StringComparison.Ordinal));
    }

    private static string Hash12() => StatusCases.Recipe.Version.Hash.Value["sha256:".Length..][..12];
}
