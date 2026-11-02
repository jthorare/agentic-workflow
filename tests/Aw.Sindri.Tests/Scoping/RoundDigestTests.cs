using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1119: the next Step reads a digest of the round log, not the raw log. A finding is closed when a later exclusion, map row or answer covers it (or its Step's newest round no longer
/// reports it), the digest counts what is still open per rule and kind, and no Step's output may drop or rewrite an earlier entry.
/// </summary>
public sealed class RoundDigestTests
{
    private const string Cov3 = "SCP-COV-003";

    private static JsonObject Entry(int round, string step, string key, string kind = "finding") => new()
    {
        ["round"] = round,
        ["step"] = step,
        ["kind"] = kind,
        ["key"] = key,
        ["text"] = "text of " + key,
        ["by"] = "step.scope." + step,
    };

    private static JsonObject Map(JsonArray? excluded = null, JsonArray? surfaces = null) => new()
    {
        ["surfaces"] = surfaces ?? [],
        ["excluded"] = excluded ?? [],
    };

    private static string[] Keys(JsonNode? list) => [.. list!.AsArray().Select(static e => e!["key"]!.GetValue<string>()).Order(StringComparer.Ordinal)];

    private static JsonNode Closed(JsonObject digest, string key) => digest["closed"]!.AsArray().Single(e => string.Equals(e!["key"]!.GetValue<string>(), key, StringComparison.Ordinal))!;

    [Fact, Trait("Lane", "unit")]
    public void A_finding_of_round_1_answered_by_an_exclusion_in_round_2_is_closed_in_round_3s_digest()
    {
        var log = new JsonArray(Entry(1, "evidence", $"{Cov3}:code:src/a.ts"), Entry(2, "evidence", $"{Cov3}:code:src/a.ts"), Entry(3, "evidence", $"{Cov3}:code:src/b.ts"));
        var map = Map(excluded: new JsonArray(new JsonObject { ["item"] = "code:src/a.ts", ["reason"] = "generated" }));

        var digest = RoundLog.Digest(log, map, answerLog: null);

        Assert.Equal(["SCP-COV-003:code:src/b.ts"], Keys(digest["open"]));
        Assert.Equal("exclusion", Closed(digest, $"{Cov3}:code:src/a.ts")["by"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_glob_exclusion_closes_every_finding_it_matches()
    {
        var log = new JsonArray(Entry(1, "evidence", $"{Cov3}:code:src/gen/a.ts"), Entry(1, "evidence", $"{Cov3}:code:src/gen/b.ts"), Entry(1, "evidence", $"{Cov3}:code:src/real/c.ts"));
        var map = Map(excluded: new JsonArray(new JsonObject { ["glob"] = "src/gen/**", ["reason"] = "generated" }));

        var digest = RoundLog.Digest(log, map, answerLog: null);

        Assert.Equal(["SCP-COV-003:code:src/real/c.ts"], Keys(digest["open"]));
        Assert.Equal(["SCP-COV-003:code:src/gen/a.ts", "SCP-COV-003:code:src/gen/b.ts"], Keys(digest["closed"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_row_for_the_item_closes_its_finding()
    {
        var log = new JsonArray(Entry(1, "challenge-log", "SCP-CHAL-001:Export a link"));
        var map = Map(surfaces: new JsonArray(new JsonObject { ["key"] = "Export a link", ["title"] = "Export a link" }));

        var digest = RoundLog.Digest(log, map, answerLog: null);

        Assert.Empty(digest["open"]!.AsArray());
        Assert.Equal("map", Closed(digest, "SCP-CHAL-001:Export a link")["by"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_that_records_the_item_closes_its_finding()
    {
        var log = new JsonArray(Entry(1, "evidence", "SCP-ACC-002:S7"));
        var answers = new JsonArray(new JsonObject { ["id"] = "A1", ["questionIds"] = new JsonArray("Q1"), ["recordedAs"] = new JsonArray("S7") });

        var digest = RoundLog.Digest(log, Map(), answers);

        Assert.Empty(digest["open"]!.AsArray());
        Assert.Equal("answer", Closed(digest, "SCP-ACC-002:S7")["by"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_finding_its_step_stopped_reporting_is_closed_as_fixed()
    {
        var log = new JsonArray(Entry(1, "check", "SCP-CITE-001:S2"), Entry(2, "check", "SCP-ACC-002:S3"));

        var digest = RoundLog.Digest(log, Map(), answerLog: null);

        Assert.Equal(["SCP-ACC-002:S3"], Keys(digest["open"]));
        Assert.Equal("fixed", Closed(digest, "SCP-CITE-001:S2")["by"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_digest_counts_what_is_open_per_rule_and_kind_and_lists_what_was_decided()
    {
        var log = new JsonArray(
            Entry(1, "evidence", $"{Cov3}:code:src/a.ts"),
            Entry(1, "evidence", $"{Cov3}:code:src/b.ts"),
            Entry(1, "evidence", "SCP-ACC-002:S3"),
            Entry(1, "evidence", $"{Cov3}:code:src/gen/x.ts"));
        var map = Map(excluded: new JsonArray(new JsonObject { ["glob"] = "src/gen/**", ["reason"] = "generated" }));
        var answers = new JsonArray(new JsonObject { ["id"] = "A1", ["questionIds"] = new JsonArray("Q1"), ["recordedAs"] = new JsonArray("Q1") });

        var digest = RoundLog.Digest(log, map, answers);

        Assert.Equal(2, digest["counts"]!["byRule"]![Cov3]!.GetValue<int>());
        Assert.Equal(1, digest["counts"]!["byRule"]!["SCP-ACC-002"]!.GetValue<int>());
        Assert.Equal(3, digest["counts"]!["byKind"]!["finding"]!.GetValue<int>());
        var decisions = digest["decisions"]!.AsArray().Select(static d => d!["kind"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["answer", "exclusion"], decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_challenge_log_step_writes_a_digest_that_lists_its_finding_as_open()
    {
        var facts = ScopingFacts.Expiry();
        facts["challengeRound"] = new JsonObject
        {
            ["verdict"] = "found",
            ["surfaces"] = new JsonArray(new JsonObject { ["title"] = "Export a link" }),
            ["behaviours"] = new JsonArray(),
        }.ToJsonString();

        var response = (await new ScopingDeskService().Handle(new ScopingDeskRequest("challenge-log", facts), TestContext.Current.CancellationToken)).Value;

        var digest = JsonNode.Parse(response.Outputs["roundDigest"])!;
        Assert.Equal(["SCP-CHAL-001:Export a link"], Keys(digest["open"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_that_adds_entries_keeps_the_earlier_ones_and_is_allowed()
    {
        var before = new JsonArray(Entry(1, "check", "SCP-CITE-001:S2"));
        var after = new JsonArray(Entry(1, "check", "SCP-CITE-001:S2"), Entry(1, "evidence", "SCP-ACC-002:S3"));

        Assert.Null(RoundLog.Refusal(before, after));
        Assert.Null(RoundLog.Refusal(before: null, after));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_that_drops_an_earlier_entry_is_refused()
    {
        var before = new JsonArray(Entry(1, "check", "SCP-CITE-001:S2"), Entry(1, "evidence", "SCP-ACC-002:S3"));
        var after = new JsonArray(Entry(1, "evidence", "SCP-ACC-002:S3"));

        var refusal = RoundLog.Refusal(before, after);

        Assert.NotNull(refusal);
        Assert.Contains("roundLog", refusal, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_that_rewrites_an_earlier_entry_is_refused()
    {
        var before = new JsonArray(Entry(1, "check", "SCP-CITE-001:S2"));
        var rewritten = Entry(1, "check", "SCP-CITE-001:S2");
        rewritten["text"] = "it was never a problem";

        var refusal = RoundLog.Refusal(before, new JsonArray(rewritten));

        Assert.NotNull(refusal);
        Assert.Contains("SCP-CITE-001:S2", refusal, StringComparison.Ordinal);
    }
}
