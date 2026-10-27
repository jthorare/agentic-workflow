using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The stop view (scoping spec §8.4): nine sections in order, the coverage header, the exact row text of a recorded limit, and "not yet" before the evidence step.</summary>
public sealed class ScopingShowTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly JsonSerializerOptions _indented = new(_json) { WriteIndented = true };

    private static async Task<ScopingView> Show(ScopingFacts facts) =>
        (await new ScopingShowService().Handle(new ScopingShowRequest(facts), CancellationToken.None)).Value;

    /// <summary>The gold facts with the code lens (found, callers not indexed) and the history lens (nothing found, tickets not linked) limited.</summary>
    internal static ScopingFacts TwoLimitedLenses() =>
        ScopingFacts.Expiry().Edit("coverage", n =>
        {
            var history = n["lenses"]![3]!;
            history["state"] = "not-searched";
            history["searched"]!["sources"] = new JsonArray(
                new JsonObject { ["id"] = "tracker", ["state"] = "unavailable", ["reason"] = "history-unlinked" },
                new JsonObject { ["id"] = "git", ["state"] = "ok" });
            history["limits"] = new JsonArray(new JsonObject { ["not"] = "ticket mentions", ["why"] = "history-unlinked" });
            history["found"] = new JsonObject { ["count"] = 0, ["ids"] = new JsonArray() };
        }).Edit("lenses", n =>
        {
            var history = n["history"]!;
            history["foundCount"] = 0;
            history["nodes"] = new JsonArray();
            history["items"] = new JsonArray();
            history["edges"] = new JsonArray();
        }).Edit("relevance", n =>
        {
            // the emptied history lens has nothing left to review
            var entries = n["entries"]!.AsArray();
            foreach (var gone in entries.Where(static e => (string?)e!["lens"] == "history").ToList())
            {
                _ = entries.Remove(gone);
            }

            n["counts"]!["reviewed"] = entries.Count;
            n["counts"]!["in"] = entries.Count;
        });

    private static ScopingSection Section(ScopingView view, string id) => view.Sections.Single(s => s.Name == id);

    [Fact, Trait("Lane", "unit")]
    public async Task The_view_has_nine_sections_in_the_order_the_owner_reads_them()
    {
        var view = await Show(ScopingFacts.Expiry());

        Assert.Equal(["checklist", "flagged", "excluded", "acceptance", "questions", "answers", "challenge", "rounds", "map"], view.Sections.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal(Enumerable.Range(1, 9), view.Sections.Select(s => s.Order));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_limited_lenses_read_2_of_4_lenses_limited_with_the_exact_row_text_and_never_nothing_found()
    {
        var view = await Show(TwoLimitedLenses());

        Assert.Equal("2 of 4 lenses limited; relevance: 26 in, 0 out, 0 noise, 0 disputed; gap search: 1 round, 0 found, 0 confirmed", view.CoverageHeader);
        var coverage = Section(view, "map").Rows.Where(r => r.Key.StartsWith("coverage:", StringComparison.Ordinal)).ToDictionary(r => r.Key, StringComparer.Ordinal);
        Assert.Equal("not searched: history-unlinked — Wait for the ticket linker (S-5B)", coverage["coverage:history"].Text);
        Assert.Equal("found 13", coverage["coverage:code"].Text);
        Assert.Equal(["not searched: index-not-built — Wait for the code index (S-9)"], coverage["coverage:code"].Lines);
        var limits = Section(view, "checklist").Rows.Where(r => r.Status == "limit").Select(r => r.Text).ToList();
        Assert.Equal(
            ["code: not searched: index-not-built — Wait for the code index (S-9)", "history: not searched: history-unlinked — Wait for the ticket linker (S-5B)"],
            limits);
        var every = JsonSerializer.Serialize(view, _json);
        Assert.DoesNotContain("nothing found", every, StringComparison.OrdinalIgnoreCase);   // "nothing-found" the state is not shown for a limited row either
        Assert.DoesNotContain("nothing-found", every, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_two_limited_lens_view_matches_its_golden()
    {
        var view = await Show(TwoLimitedLenses());

        Golden.Match(JsonSerializer.Serialize(view, _indented) + "\n", typeof(ScopingShowTests).Assembly, "Golden/Scoping/two-limited-lenses.view.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_facts_match_the_stop_golden_with_the_packet_assembled()
    {
        var facts = ScopingFacts.Expiry();
        facts["scopePacket"] = "\"sha256:" + new string('a', 64) + "\"";

        var view = await Show(facts);

        Assert.True(view.OpenInContext.Enabled);
        Golden.Match(JsonSerializer.Serialize(view, _indented) + "\n", typeof(ScopingShowTests).Assembly, "Golden/Scoping/expiry-stop.view.json", Normalization.None);
        Golden.Match(JsonSerializer.Serialize(view, _indented) + "\n", typeof(ScopingShowTests).Assembly, "Golden/Api/workflow-instance-scoping/expiry-stop.json", Normalization.None);   // API2 serves this view as is
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("code", "unavailable", "commit-not-held", "SND-CONTEXT-110", "not searched: commit-not-held — Fetch the commit into the profile repo's checkout, then regather")]
    [InlineData("product", "not-configured", "not-configured", null, "not searched: not-configured — Name the source in the profile, then regather")]
    public async Task Each_recorded_limit_reads_with_its_fix_line_from_the_one_constant_map(string lens, string state, string reason, string? code, string expected)
    {
        var view = await Show(ScopingCheckTests.Limited(lens, state, reason, code, rowState: "not-searched", count: 0));

        Assert.Equal(expected, Section(view, "map").Rows.Single(r => r.Key == $"coverage:{lens}").Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("null", "not searched: never synced — a sync starts shortly")]
    [InlineData("""{"state":"queued","jobId":"JOB-1","at":"2026-10-18T09:00:00Z"}""", "not searched: never synced — sync queued")]
    [InlineData("""{"state":"running","jobId":"JOB-1","at":"2026-10-18T09:00:00Z"}""", "not searched: never synced — sync running")]
    [InlineData("""{"state":"finished","jobId":"JOB-1","at":"2026-10-18T09:00:00Z"}""", "not searched: never synced — synced since; rescope to include it")]
    [InlineData("""{"state":"failed","jobId":"JOB-1","at":"2026-10-18T09:00:00Z","code":"SND-CONTEXT-170","retryAt":"2026-10-18T10:00:00Z"}""", "not searched: never synced — the sync failed (SND-CONTEXT-170); retrying at 2026-10-18T10:00:00Z")]
    [InlineData("""{"state":"failed","jobId":"JOB-1","at":"2026-10-18T09:00:00Z","code":"SND-CONTEXT-170","failures":3}""", "not searched: never synced — the sync failed (SND-CONTEXT-170); stopped after 3 failures: run `sindri context sources sync` once it's fixed")]
    public async Task Not_synced_reads_by_the_sync_pair_in_five_sentences(string sync, string expected)
    {
        var facts = ScopingCheckTests.Limited("history", "unavailable", "not-synced", "SND-CONTEXT-161", rowState: "not-searched", count: 0)
            .Edit("coverage", n => n["lenses"]![3]!["searched"]!["sources"]![0]!["sync"] = JsonNode.Parse(sync));

        var view = await Show(facts);

        Assert.Equal(expected, Section(view, "map").Rows.Single(r => r.Key == "coverage:history").Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_nothing_found_row_with_a_thin_map_warns_with_the_map_coverage_and_never_refuses()
    {
        var facts = ScopingFacts.Expiry().Edit("coverage", n =>
        {
            var row = n["lenses"]![1]!;
            row["state"] = "nothing-found";
            row["found"] = new JsonObject { ["count"] = 0, ["ids"] = new JsonArray() };
            row["mapCoverage"] = new JsonObject { ["mapped"] = 1, ["new"] = 0, ["unmapped"] = 3 };
        });

        var view = await Show(facts);

        var warning = Section(view, "checklist").Rows.Single(r => r.Status == "warn");
        Assert.Equal("product: nothing found, map thin: 1 of 4 nodes mapped", warning.Text);
        Assert.Equal("SCP-COV-005", warning.Key);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_the_evidence_step_the_sections_that_need_its_facts_say_not_yet_and_open_in_context_says_why()
    {
        var facts = ScopingFacts.Expiry().Without("challenge").Without("rounds").Without("runs").Without("decision");

        var view = await Show(facts);

        Assert.Equal(ScopeStage.Draft, view.Stage);
        Assert.False(view.OpenInContext.Enabled);
        Assert.Equal("Available once the evidence step assembles the packet", view.OpenInContext.Reason);
        var challenge = Section(view, "challenge");
        Assert.Equal(ScopingSectionState.NotYet, challenge.State);
        Assert.Contains("step.scope.challenge", challenge.Note, StringComparison.Ordinal);
        Assert.Equal(ScopingSectionState.NotYet, Section(view, "rounds").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_section_says_so()
    {
        var view = await Show(ScopingFacts.Expiry());

        var flagged = Section(view, "flagged");
        Assert.Equal(ScopingSectionState.Empty, flagged.State);
        Assert.Equal("No flagged items", flagged.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Flagged_items_sit_beside_the_unresolved_findings_with_their_rule_and_why()
    {
        var facts = ScopingFacts.Expiry().Edit("scopeMap", n =>
        {
            n["flagged"]!.AsArray().Add(JsonNode.Parse("""{"item":"S99","reason":"no citation resolves"}"""));
            n["unresolved"]!.AsArray().Add(JsonNode.Parse("""{"rule":"SCP-ACC-005","item":"S12","why":"no source says what happens to revoked links"}"""));
        });

        var rows = Section(await Show(facts), "flagged").Rows;

        Assert.Contains(rows, r => r.Key == "S99" && r.Text.Contains("no citation resolves", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Key == "S12" && r.Text.Contains("SCP-ACC-005", StringComparison.Ordinal) && r.Text.Contains("no source says what happens", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_answer_echo_is_last_batch_first_and_the_acceptance_items_carry_the_answers_behind_them()
    {
        var view = await Show(ScopingFacts.Expiry());

        Assert.Equal(["A2", "A1"], Section(view, "answers").Rows.Select(r => r.Key), StringComparer.Ordinal);
        var ac2 = Section(view, "acceptance").Rows.Single(r => r.Key == "AC-2");
        Assert.Contains(ac2.Lines, l => l.StartsWith("answer A1:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_challenger_rounds_name_the_model_and_the_vendor_that_ran_each_and_a_desk_run_warns_first()
    {
        var gold = await Show(ScopingFacts.Expiry());
        var challenge = Section(gold, "challenge");
        Assert.Contains(challenge.Rows, r => r.Text.Contains("fixture-challenger-model", StringComparison.Ordinal) && r.Text.Contains("fixture", StringComparison.Ordinal));

        var desk = ScopingFacts.Expiry().Edit("runs", n => n["runs"]![1]!["via"] = "desk");
        var warnings = Section(await Show(desk), "checklist").Rows.Where(r => r.Status == "warn").Select(r => r.Text).ToList();
        Assert.Contains("challenger ran at the desk: model self-reported", warnings, StringComparer.Ordinal);
    }
}
