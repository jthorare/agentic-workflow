using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>The status words the engine will print (WF section 6.4, API spec section 3.8): answers per park, the header detail, ASCII, and no invented state words.</summary>
public sealed class WorkflowViewStatusWordsTests
{
    private static Regex Re(string pattern) => new(pattern, RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    [Fact, Trait("Lane", "unit")]
    public void A_drift_park_takes_no_answer_and_offers_the_protected_reapproval_beside_takeover_and_cancel()
    {
        var status = WorkflowViewFixtures.StatusParkedDrift();

        Assert.Null(status.Answer);
        Assert.Equal(["takeover", "cancel", "approve"], status.Commands.Select(c => StatusVerbNames.Of(c.Verb)), StringComparer.Ordinal);
        var approve = Assert.Single(status.Commands, c => c.Verb == StatusVerb.Approve);
        Assert.Equal(ActionClass.Desk, approve.Class);
        Assert.Equal("ApproveRecipe", approve.ActionId.Value);
        Assert.Equal($"sindri recipe approve {status.Header.Recipe.Value}@{status.Header.Hash12}", approve.Cli);
        Assert.Null(approve.Confirm);
        Assert.Contains("the approval resumes", status.Next, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_escalation_escalates_below_its_threshold()
    {
        var status = WorkflowViewFixtures.StatusEscalatedGate();
        var match = Re("scored (?<score>\\d\\.\\d+), below (?<limit>\\d\\.\\d+)").Match(status.Next!);

        Assert.True(match.Success, status.Next);
        Assert.True(double.Parse(match.Groups["score"].Value, CultureInfo.InvariantCulture) < double.Parse(match.Groups["limit"].Value, CultureInfo.InvariantCulture));
        var view = WorkflowViewFixtures.EscalatedGate();
        var gate = Assert.Single(view.Current.Gates);
        Assert.Equal(0.8, gate.Threshold);
        var row = WorkflowViewFixtures.RootList().Instances.Single(r => r.Address.Value == status.Header.Address.Value);
        Assert.DoesNotContain("0.91", row.EngineOwned!.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(view.Facts, f => f.Summary.Contains("0.91", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_park_answers_with_text_and_a_desk_step_answers_through_complete()
    {
        Assert.Equal("sindri workflow answer ITM-01JKE/project/1 --text <answer>", WorkflowViewFixtures.StatusNeedsAnswerStep().Answer!.Cli);

        var desk = WorkflowViewFixtures.StatusDeskTask();
        Assert.Null(desk.Answer);
        Assert.Equal("complete", StatusVerbNames.Of(desk.Commands[0].Verb));
        Assert.StartsWith("sindri workflow complete ITM-01JK2/triage-alert/1 --run 2.1 ", desk.Commands[0].Cli, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_next_names_a_round_only_when_its_recipe_has_a_loop_through_the_state()
    {
        Assert.DoesNotContain("round", WorkflowViewFixtures.StatusDeskTask().Next, StringComparison.Ordinal);
        Assert.Contains("round 1/2", WorkflowViewFixtures.DeskRoundTrip().Current.Status.Next, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_header_detail_is_set_only_for_an_engine_reason_a_desk_task_or_an_outcome_that_differs_from_the_state()
    {
        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            var header = status.Header;
            if (header.Detail is null)
            {
                continue;
            }

            var allowed = header.Detail == "desk task" || header.State == "parked" || header.State == "escalated" || (header.State == "done" && header.Detail != "done");
            Assert.True(allowed, $"{header.Address.Value}  {header.State}: {header.Detail}");
        }

        Assert.Null(WorkflowViewFixtures.ProjectBuild().Current.Status.Header.Detail);
        Assert.Null(WorkflowViewFixtures.IncidentLoop().Current.Status.Header.Detail);
        Assert.Null(WorkflowViewFixtures.TransitionList().Current.Status.Header.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_status_text_is_plain_ascii()
    {
        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            // the instance's name is the recipe author's template over the user's own values (WF 3.11), so its line is the one place user text passes through
            foreach (var text in new[] { string.Join('\n', status.Text.Split('\n').Where(static l => !l.StartsWith("  name ", StringComparison.Ordinal))), status.Next ?? string.Empty, status.Header.Detail ?? string.Empty, status.Answer?.Cli ?? string.Empty })
            {
                Assert.True(text.All(char.IsAscii), $"{status.Header.Address.Value}: {text}");
            }
        }

        Assert.Contains("sha256:1c0e...", WorkflowViewFixtures.StatusDeskTask().Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_incident_row_says_what_is_happening_and_its_status_says_what_to_do_so_they_differ()
    {
        var row = WorkflowViewFixtures.RootList().Instances.Single(r => r.Address.Value.Contains("incident-response", StringComparison.Ordinal));

        Assert.NotEqual(row.Now, WorkflowViewFixtures.IncidentLoop().Current.Status.Next, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_truncated_fact_has_its_own_key_reachable_from_a_row() =>
        Assert.NotEqual(WorkflowViewFixtures.Fact("payload").Key, WorkflowViewFixtures.Fact("truncated").Key, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void No_row_or_status_text_uses_queued_as_a_state_word()
    {
        var words = WorkflowViewFixtures.RootList().Instances.Select(r => r.Now)
            .Concat(WorkflowViewFixtures.AllStatuses().Select(s => s.Next ?? string.Empty))
            .Concat(WorkflowViewFixtures.AllDetails().SelectMany(d => d.Facts.Select(f => f.Summary)));

        Assert.DoesNotContain(words, w => w.Contains("queued", StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wait_park_is_answered_with_resume_and_a_step_park_with_text_and_neither_names_a_child_it_has_not_mirrored()
    {
        var statuses = WorkflowViewFixtures.AllStatuses();
        var wait = Assert.Single(statuses, s => s.Answer is { Kind: AnswerKind.Resume });
        Assert.EndsWith("--resume [--text <answer>]", wait.Answer!.Cli, StringComparison.Ordinal);
        Assert.Equal(ActionClass.Desk, wait.Answer.Class);
        // a child pointer with no child listed still says "the child"; the rest name none
        Assert.All(statuses.Where(s => s.Child is null && s.Answer?.Via is null && s.Header.State == "parked"), s => Assert.DoesNotContain(") in ", s.Next, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_task_prints_its_timeout_as_desk_like_the_shipped_status_layout()
    {
        var desks = WorkflowViewFixtures.AllStatuses().Where(s => s.Commands.Any(c => c.Verb == StatusVerb.Complete) && s.Timeout is not null).ToList();
        Assert.NotEmpty(desks);
        Assert.All(desks, s => Assert.Matches(@"timeout   parks in [0-9dhm ]+ \(desk\)\n", s.Text));
    }
}
