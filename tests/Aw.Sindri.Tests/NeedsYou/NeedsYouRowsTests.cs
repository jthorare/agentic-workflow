using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The needs-you rows per kind (P4 plan §1.3, G8, G12; UI spec E-7, E-16).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowsTests
{
    private static readonly DateTimeOffset _t = KnownInstants.Epoch;
    private static readonly ServiceName _service = ServiceName.From("heartbeat");
    private static readonly ProfileHash _approved = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _live = ProfileHash.From("3f1c2a" + new string('0', 58));
    private readonly PassThroughScrubber _plain = new();

    [Fact]
    public void A_decision_is_a_pulsing_recorded_only_row_that_decides()
    {
        var decision = A.Decision().About(A.FirstItemId, 3, RepoName.From("agentic-workflow")).Build();

        var row = NeedsYouRows.ForDecision(decision, _plain).Value;

        Expect.Equal(
            (NeedsYouRowId.From("decision:" + A.FirstDecisionId.Value), NeedsYouKind.Decision, "decision · re-scope", new NeedsYouSubject(SubjectKind.Decision, SubjectId.From(A.FirstDecisionId.Value))),
            (row.Id, row.Kind, row.Label, row.Subject));
        Expect.Equal((A.FirstItemId, RepoName.From("agentic-workflow"), "Split the work into two items.", "re-scope request from session:cli"), (row.Item!.Value, row.Repo!.Value, row.Title, row.Reason));
        Expect.Equal((Urgency.Pulse, decision.FiledAt, true, EmptyText.RecordedOnly()), (row.Urgency, row.Since, row.RecordedOnly, row.Notice));
        Expect.Equal((ActionId.From("Decide"), "sindri decide " + A.FirstDecisionId.Value + " accept"), (row.PrimaryAction, row.Cli));
        Expect.Sequence([ActionId.From("Decide")], row.Actions);
        Expect.True(row.AnswerKind is null);
    }

    [Fact]
    public void A_decision_title_is_the_first_line_of_the_text_cut_to_120()
    {
        var text = new string('t', 200);
        var decision = A.Decision().Build() with { Text = text };

        var title = NeedsYouRows.ForDecision(decision, _plain).Value.Title;

        Expect.Equal(120, title.Length);
        Expect.True(title.EndsWith('…'));
    }

    [Fact]
    public void A_decision_label_follows_the_request_kind()
    {
        var decision = A.Decision().Build() with { RequestKind = RequestKind.Handback };

        var row = NeedsYouRows.ForDecision(decision, _plain).Value;

        Expect.Equal(("decision · hand back", "hand back request from session:cli"), (row.Label, row.Reason));
    }

    [Fact]
    public void A_failure_is_a_coloured_row_that_runs_the_service_again()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "SND-SCHED-001", _t.AddSeconds(2)), Paused: false, Sequence: 4);

        var row = NeedsYouRows.ForFailure(failed, _t, _plain).Value;

        Expect.Equal(
            (NeedsYouRowId.From("failure:service:heartbeat"), NeedsYouKind.Failure, "failure · heartbeat", new NeedsYouSubject(SubjectKind.Service, SubjectId.From("heartbeat"))),
            (row.Id, row.Kind, row.Label, row.Subject));
        Expect.Equal(("heartbeat failed", "SND-SCHED-001: A background service failed", Urgency.Colour, _t), (row.Title, row.Reason, row.Urgency, row.Since));
        Expect.Equal((ActionId.From("RunService"), "sindri services run heartbeat", false, null), (row.PrimaryAction, row.Cli, row.RecordedOnly, row.Notice));
        Expect.Sequence([ActionId.From("RunService"), ActionId.From("PauseService")], row.Actions);
        Expect.True(row.Item is null && row.Repo is null);
    }

    [Fact]
    public void A_built_in_failure_offers_run_only_since_a_built_in_cannot_be_paused()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "SND-SCHED-001", _t), Paused: false, Sequence: 4);

        var row = NeedsYouRows.ForFailure(failed, _t, _plain, builtIn: true).Value;

        Expect.Equal((ActionId.From("RunService"), "sindri services run heartbeat"), (row.PrimaryAction, row.Cli));
        Expect.Sequence([ActionId.From("RunService")], row.Actions);
    }

    [Fact]
    public void A_failure_for_a_service_the_build_lacks_cannot_be_run_and_points_at_the_profile()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "SND-SVC-404", _t), Paused: false, Sequence: 4);

        var row = NeedsYouRows.ForFailure(failed, _t, _plain).Value;

        Expect.Equal(("sindri profile validate", true), (row.Cli, row.PrimaryAction is null));
        Expect.Sequence([ActionId.From("PauseService")], row.Actions);
        Assert.Contains("remove or rename", row.Reason, StringComparison.Ordinal);
        Assert.StartsWith("SND-SVC-404", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_without_a_code_says_which_slot_failed()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, Code: null, _t), Paused: false, Sequence: 4);

        Expect.Equal("failed at 2026-01-01T00:00:00Z", NeedsYouRows.ForFailure(failed, _t, _plain).Value.Reason);
    }

    [Fact]
    public void A_failure_with_an_unknown_code_shows_the_code_alone()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "X-1", _t), Paused: false, Sequence: 4);

        Expect.Equal("X-1", NeedsYouRows.ForFailure(failed, _t, _plain).Value.Reason);
    }

    [Fact]
    public void A_failure_row_for_a_run_that_didnt_fail_is_refused()
    {
        var ok = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Ok, Code: null, _t), Paused: false, Sequence: 4);

        Expect.True(NeedsYouRows.ForFailure(ok, _t, _plain).IsError);
    }

    [Fact]
    public void A_drifted_profile_is_a_calm_row_that_approves_the_live_hash()
    {
        var drifted = Profile(drifted: true, "services.heartbeat.every", "services.index");

        var row = NeedsYouRows.ForProfile(drifted, _t, _plain).Value!;

        Expect.Equal(
            (NeedsYouRowId.From("profile:drift"), NeedsYouKind.Profile, "profile · drifted", new NeedsYouSubject(SubjectKind.Profile, SubjectId.From(_live.Value))),
            (row.Id, row.Kind, row.Label, row.Subject));
        Expect.Equal(("The profile changed since approval", "2 key paths changed: services.heartbeat.every, services.index", Urgency.Calm, _t), (row.Title, row.Reason, row.Urgency, row.Since));
        Expect.Equal((ActionId.From("ApproveProfile"), "sindri profile approve " + _live.Value, false), (row.PrimaryAction, row.Cli, row.RecordedOnly));
        Expect.Sequence([ActionId.From("ApproveProfile")], row.Actions);
    }

    [Fact]
    public void A_drifted_profile_lists_only_the_first_three_paths()
    {
        var row = NeedsYouRows.ForProfile(Profile(drifted: true, "a", "b", "c", "d", "e"), _t, _plain).Value!;

        Expect.Equal("5 key paths changed: a, b, c", row.Reason);
        Expect.Equal("1 key path changed: a", NeedsYouRows.ForProfile(Profile(drifted: true, "a"), _t, _plain).Value!.Reason);
    }

    [Fact]
    public void A_profile_that_matches_or_has_no_live_hash_has_no_row()
    {
        Expect.True(NeedsYouRows.ForProfile(Profile(drifted: false), _t, _plain).Value is null);
        Expect.True(NeedsYouRows.ForProfile(Profile(drifted: true) with { Live = null }, _t, _plain).Value is null);
    }

    [Fact]
    public void Every_row_primary_action_is_one_of_its_actions_and_never_copy()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "SND-SCHED-001", _t), Paused: false, Sequence: 4);
        var rows = new[]
        {
            NeedsYouRows.ForDecision(A.Decision().Build(), _plain).Value,
            NeedsYouRows.ForFailure(failed, _t, _plain).Value,
            NeedsYouRows.ForProfile(Profile(drifted: true, "a"), _t, _plain).Value!,
        };

        foreach (var row in rows)
        {
            Expect.True(row.PrimaryAction is { } primary && row.Actions.Contains(primary) && !primary.Value.StartsWith("copy", StringComparison.Ordinal));
            Expect.True(row.Cli.StartsWith("sindri ", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Free_text_is_scrubbed_and_escaped()
    {
        var failed = new ServiceRow(_service, new LastRun(_t, ServiceOutcome.Failed, "SECRET\u001b[31m", _t), Paused: false, Sequence: 4);

        var reason = NeedsYouRows.ForFailure(failed, _t, new MaskScrubber("SECRET")).Value.Reason;

        Expect.Equal("[masked]\\x1B[31m", reason);
    }

    private static ProfileRow Profile(bool drifted, params string[] paths) =>
        new(_approved, _t, _live, _t, drifted, paths, [_service], Sequence: 5);

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
