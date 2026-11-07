using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Testing.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The result-file table (skill flows spec §3.3), one test per row: what a turn end means, decided from the file alone so no workflow runs.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillResultReaderTests
{
    private static readonly DateTimeOffset _started = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
    private const string Findings = "/home/u/.agentic-workflow/acme-api/reviews/42.json";

    private static SkillResultInput Input(SkillBlockManifest block, string? json, DateTimeOffset? modified = null, DateTimeOffset? lastHandled = null, string skill = "review", Func<string, bool>? exists = null) =>
        new(json is null ? null : new SkillResultFile(Encoding.UTF8.GetBytes(json), modified ?? _started.AddMinutes(30)), skill, block, _started, lastHandled, exists ?? (static _ => true));

    private static SkillResultInput Review(string? json, DateTimeOffset? modified = null, DateTimeOffset? lastHandled = null, string skill = "review") =>
        Input(SkillFixtures.Review, json, modified, lastHandled, skill);

    private static string Autoplan(string artifacts, string verdict = "NEEDS_WORK", string featureDir = "/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits") =>
        $$"""{"skill":"autoplan","verdict":"{{verdict}}","feature_dir":"{{featureDir}}","artifacts":[{{artifacts}}]}""";

    [Fact]
    public void A_finished_result_completes_with_the_facts_and_the_mapped_outcome()
    {
        var decision = SkillResultReader.Decide(Review($$"""{"skill":"review","verdict":"NEEDS_CHANGES","review_file":"{{Findings}}"}"""));

        Assert.Equal(SkillResultKind.Complete, decision.Kind);
        Assert.Equal("needsChanges", decision.Outcome);
        Assert.Equal(Findings, Assert.Single(decision.Facts).Value);
        Assert.Equal("fact:reviewFindings", Assert.Single(decision.Facts).Key);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void An_output_taken_from_an_artifact_is_matched_by_file_name()
    {
        const string Report = "/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits/consolidated-review.md";
        var json = Autoplan($"\"{Report}\",\"/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits/arch-review.md\"");

        var decision = SkillResultReader.Decide(Input(SkillFixtures.Autoplan, json, skill: "autoplan"));

        Assert.Equal(SkillResultKind.Complete, decision.Kind);
        Assert.Equal("needsWork", decision.Outcome);
        Assert.Equal(Report, decision.Facts["fact:autoplanReport"]);
        Assert.Equal("/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits", decision.Facts["fact:autoplanFeatureDir"]);
    }

    [Fact]
    public void A_block_with_no_outcome_source_completes_on_its_success_outcome()
    {
        var json = """{"skill":"officeHours","feature_dir":"/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits"}""";

        var decision = SkillResultReader.Decide(Input(SkillFixtures.OfficeHours, json, skill: "officeHours"));

        Assert.Equal((SkillResultKind.Complete, "done"), (decision.Kind, decision.Outcome));
    }

    [Fact]
    public void A_result_with_waiting_is_the_owner_waiting_with_the_question()
    {
        var decision = SkillResultReader.Decide(Input(SkillFixtures.OfficeHours, """{"skill":"officeHours","waiting":"Who calls this API: browsers, servers, or both?"}""", skill: "officeHours"));

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.Question), (decision.Kind, decision.Reason));
        Assert.Equal("Who calls this API: browsers, servers, or both?", decision.Text);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void No_file_is_the_owner_waiting_stopped_without_a_result()
    {
        var decision = SkillResultReader.Decide(Review(json: null));

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.NoResult), (decision.Kind, decision.Reason));
        Assert.Null(decision.Error);
    }

    [Fact]
    public void A_file_written_before_this_launch_started_is_no_result()
    {
        var decision = SkillResultReader.Decide(Review($$"""{"skill":"review","verdict":"PASS","review_file":"{{Findings}}"}""", modified: _started.AddMinutes(-5)));

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.NoResult), (decision.Kind, decision.Reason));
    }

    [Fact]
    public void The_same_file_as_at_the_last_turn_end_is_still_waiting()
    {
        var written = _started.AddMinutes(30);

        var decision = SkillResultReader.Decide(Review("""{"skill":"review","waiting":"Which PR?"}""", modified: written, lastHandled: written.AddMinutes(1)));

        Assert.Equal(SkillResultKind.StillWaiting, decision.Kind);
    }

    [Fact]
    public void A_file_newer_than_the_last_turn_end_is_read_again()
    {
        var decision = SkillResultReader.Decide(Review($$"""{"skill":"review","verdict":"PASS","review_file":"{{Findings}}"}""", modified: _started.AddMinutes(45), lastHandled: _started.AddMinutes(30)));

        Assert.Equal(SkillResultKind.Complete, decision.Kind);
    }

    [Fact]
    public void A_file_that_is_not_JSON_is_unreadable_with_the_parse_error_SND_SKILL_001()
    {
        var decision = SkillResultReader.Decide(Review("{\"skill\":"));

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.Unreadable), (decision.Kind, decision.Reason));
        Assert.Equal(SindriErrors.SkillResultUnreadable.Code, decision.Error?.Code);
        Assert.False(string.IsNullOrWhiteSpace(decision.Detail));
    }

    [Fact]
    public void A_result_from_another_skill_waits_and_names_it_SND_SKILL_002()
    {
        var decision = SkillResultReader.Decide(Review("""{"skill":"autoplan","verdict":"PASS"}"""));

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.Unreadable), (decision.Kind, decision.Reason));
        Assert.Equal(SindriErrors.SkillResultWrongSkill.Code, decision.Error?.Code);
        Assert.Equal("autoplan", decision.Detail);
    }

    [Fact]
    public void A_verdict_the_map_lacks_fails_the_Step_SND_SKILL_003()
    {
        var decision = SkillResultReader.Decide(Input(SkillFixtures.Autoplan, Autoplan("\"/p/consolidated-review.md\"", verdict: "MAYBE"), skill: "autoplan"));

        Assert.Equal(SkillResultKind.StepFailed, decision.Kind);
        Assert.Equal(SindriErrors.SkillVerdictUnmapped.Code, decision.Error?.Code);
        Assert.Contains("autoplan said 'MAYBE', which its block.yaml doesn't map", decision.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mapped_artifact_the_result_lacks_fails_the_Step_and_lists_what_it_has_SND_SKILL_004()
    {
        var decision = SkillResultReader.Decide(Input(SkillFixtures.Autoplan, Autoplan("\"/p/arch-review.md\",\"/p/devex-review.md\"", verdict: "PASS"), skill: "autoplan"));

        Assert.Equal(SkillResultKind.StepFailed, decision.Kind);
        Assert.Equal(SindriErrors.SkillOutputInvalid.Code, decision.Error?.Code);
        Assert.Contains("autoplan's result has no artifact 'consolidated-review.md' (it has: arch-review.md, devex-review.md)", decision.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_partial_result_without_the_success_only_outputs_ends_on_its_outcome_and_keeps_the_ones_it_has()
    {
        var input = Input(SkillFixtures.BugFixOrchestrator, """{"skill":"bugFixOrchestrator","status":"partial","branch":"fix/x"}""", skill: "bugFixOrchestrator");

        var decision = SkillResultReader.Decide(input);

        Assert.Equal((SkillResultKind.Complete, "partial"), (decision.Kind, decision.Outcome));
        Assert.Equal("fix/x", decision.Facts["fact:bugFixOrchestratorBranch"]);
        Assert.False(decision.Facts.ContainsKey("fact:bugFixOrchestratorPr"));
        Assert.False(decision.Facts.ContainsKey("fact:bugFixOrchestratorResolution"));
    }

    [Fact]
    public void A_resolved_result_without_the_pr_still_fails_the_Step_SND_SKILL_004()
    {
        var input = Input(SkillFixtures.BugFixOrchestrator, """{"skill":"bugFixOrchestrator","status":"resolved","branch":"fix/x"}""", skill: "bugFixOrchestrator");

        var decision = SkillResultReader.Decide(input);

        Assert.Equal((SkillResultKind.StepFailed, SindriErrors.SkillOutputInvalid.Code), (decision.Kind, decision.Error?.Code));
    }

    [Fact]
    public void A_mapped_top_level_key_the_result_lacks_fails_the_Step_SND_SKILL_004()
    {
        var decision = SkillResultReader.Decide(Review("""{"skill":"review","verdict":"PASS"}"""));

        Assert.Equal((SkillResultKind.StepFailed, SindriErrors.SkillOutputInvalid.Code), (decision.Kind, decision.Error?.Code));
    }

    [Fact]
    public void A_relative_path_output_fails_the_Step_SND_SKILL_004()
    {
        var decision = SkillResultReader.Decide(Review("""{"skill":"review","verdict":"PASS","review_file":"reviews/42.json"}"""));

        Assert.Equal((SkillResultKind.StepFailed, SindriErrors.SkillOutputInvalid.Code), (decision.Kind, decision.Error?.Code));
    }

    [Fact]
    public void A_path_output_that_is_not_on_disk_fails_the_Step_SND_SKILL_004()
    {
        var input = Input(SkillFixtures.Review, $$"""{"skill":"review","verdict":"PASS","review_file":"{{Findings}}"}""", exists: static _ => false);

        var decision = SkillResultReader.Decide(input);

        Assert.Equal((SkillResultKind.StepFailed, SindriErrors.SkillOutputInvalid.Code), (decision.Kind, decision.Error?.Code));
    }

    [Fact]
    public void A_path_output_with_a_newline_fails_the_Step_SND_SKILL_004()
    {
        var decision = SkillResultReader.Decide(Review("""{"skill":"review","verdict":"PASS","review_file":"/home/u/reviews/42.json\n/etc/passwd"}"""));

        Assert.Equal((SkillResultKind.StepFailed, SindriErrors.SkillOutputInvalid.Code), (decision.Kind, decision.Error?.Code));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void An_empty_or_null_waiting_is_not_waiting_the_rest_of_the_result_decides(string waiting)
    {
        var decision = SkillResultReader.Decide(Review($$"""{"skill":"review","waiting":{{waiting}},"verdict":"PASS","review_file":"{{Findings}}"}"""));

        Assert.Equal((SkillResultKind.Complete, "pass"), (decision.Kind, decision.Outcome));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    [InlineData("3")]
    [InlineData("{\"q\":1}")]
    public void A_waiting_that_is_not_a_string_is_not_a_question_the_rest_of_the_result_decides(string waiting)
    {
        var decision = SkillResultReader.Decide(Review($$"""{"skill":"review","waiting":{{waiting}},"verdict":"PASS","review_file":"{{Findings}}"}"""));

        Assert.Equal((SkillResultKind.Complete, "pass"), (decision.Kind, decision.Outcome));
    }

    [Fact]
    public void Waiting_is_checked_before_the_outcome_map_so_an_unmapped_status_is_never_SND_SKILL_003()
    {
        var input = Input(SkillFixtures.BugFixOrchestrator, """{"skill":"bugFixOrchestrator","status":"waiting","waiting":"Is the expected behaviour X or Y?"}""", skill: "bugFixOrchestrator");

        var decision = SkillResultReader.Decide(input);

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.Question), (decision.Kind, decision.Reason));
        Assert.Null(decision.Error);
    }

    [Fact]
    public void A_file_older_than_the_last_prompt_raises_no_row_because_the_turn_is_still_working()
    {
        var written = _started.AddMinutes(30);

        var decision = SkillResultReader.Decide(Review("""{"skill":"review","waiting":"Which PR?"}""", modified: written) with { LastPrompt = written.AddMinutes(2) });

        Assert.Equal(SkillResultKind.Working, decision.Kind);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void No_file_after_a_later_prompt_is_working_not_no_result()
    {
        var decision = SkillResultReader.Decide(Review(json: null) with { LastPrompt = _started.AddMinutes(10) });

        Assert.Equal(SkillResultKind.Working, decision.Kind);
    }

    [Fact]
    public void A_file_written_after_the_last_prompt_can_make_a_waiting_row()
    {
        var decision = SkillResultReader.Decide(Review("""{"skill":"review","waiting":"Which PR?"}""", modified: _started.AddMinutes(30)) with { LastPrompt = _started.AddMinutes(20) });

        Assert.Equal((SkillResultKind.OwnerWaiting, SkillWaitReason.Question), (decision.Kind, decision.Reason));
    }

    [Fact]
    public void The_unchanged_file_is_still_waiting_only_when_no_prompt_came_after_the_last_turn_end()
    {
        var written = _started.AddMinutes(30);
        var handled = written.AddMinutes(1);

        var quiet = SkillResultReader.Decide(Review("""{"skill":"review","waiting":"Which PR?"}""", modified: written, lastHandled: handled) with { LastPrompt = written.AddMinutes(-1) });
        var prompted = SkillResultReader.Decide(Review("""{"skill":"review","waiting":"Which PR?"}""", modified: written, lastHandled: handled) with { LastPrompt = handled.AddMinutes(1) });

        Assert.Equal(SkillResultKind.StillWaiting, quiet.Kind);
        Assert.Equal(SkillResultKind.Working, prompted.Kind);   // a prompt after the turn end means a new turn is running; the old file says nothing new
    }
}
