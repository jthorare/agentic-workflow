using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Scoping;

public sealed class ScopingChecksTests
{
    private static async Task<IReadOnlyList<CheckResult>> Run(Dictionary<string, byte[]> files)
    {
        var read = await new InMemoryScopingPacketSource().With("pkt", files).Read("pkt", CancellationToken.None);
        return ScopingChecks.Run(read.Value, PacketFiles.Json);
    }

    private static bool IsLensRule(CheckResult check) =>
        string.Equals(check.Rule, ScopingRules.LensesSchema, StringComparison.Ordinal) || string.Equals(check.Rule, ScopingRules.LensIds, StringComparison.Ordinal);

    private static IEnumerable<CheckResult> Failures(IEnumerable<CheckResult> checks, string rule) =>
        checks.Where(c => c.Rule == rule && c.State == CheckState.Fail);

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_packet_passes_every_scoping_rule()
    {
        var checks = await Run(A.ScopingPacket().Build());

        Assert.Equal(
            [ScopingRules.FilesPresent, ScopingRules.NoUnlisted, ScopingRules.WellFormed, ScopingRules.LensesSchema, ScopingRules.LensIds, ScopingRules.GraphShape],
            checks.Select(static c => c.Rule), StringComparer.Ordinal);
        Assert.All(checks, static c => Assert.Equal(CheckState.Pass, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_file_fails_PKT_SCP_001_and_leaves_the_lens_rules_unchecked_when_it_is_lenses_json()
    {
        var checks = await Run(A.ScopingPacket().Without("lenses.json").Without("brief.md").Build());

        Assert.Equal(["file brief.md", "file lenses.json"], Failures(checks, ScopingRules.FilesPresent).Select(static c => c.Subject), StringComparer.Ordinal);
        Assert.All(checks.Where(static c => IsLensRule(c)), static c => Assert.Equal(CheckState.Unchecked, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_relevance_and_gap_search_files_are_required_files_of_the_fifteen()
    {
        var checks = await Run(A.ScopingPacket().Without("relevance.json").Without("gap-search.json").Build());

        Assert.Equal(["file gap-search.json", "file relevance.json"], Failures(checks, ScopingRules.FilesPresent).Select(static c => c.Subject), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_entry_the_kind_does_not_list_fails_PKT_SCP_002_with_a_fix()
    {
        var checks = await Run(A.ScopingPacket().With("notes.txt", "x").With("attachments/a.png", "x").Build());

        var failures = Failures(checks, ScopingRules.NoUnlisted).ToList();
        Assert.Equal(["entry attachments/", "entry notes.txt"], failures.Select(static c => c.Subject), StringComparer.Ordinal);
        Assert.All(failures, static f => Assert.False(string.IsNullOrEmpty(f.Fix)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("scope-map.json", "{")]
    [InlineData("scope-map.json", "[]")]
    [InlineData("coverage.json", "{\"a\":1,\"a\":2}")]
    [InlineData("size.json", "{\"a\":[null]}")]
    [InlineData("answers.jsonl", "{\"a\":1}\nnot json\n")]
    [InlineData("rounds.jsonl", "[1]\n")]
    [InlineData("relevance.json", "[]")]
    [InlineData("gap-search.json", "{")]
    public async Task A_file_that_is_not_the_shape_its_format_names_fails_PKT_SCP_003(string name, string text)
    {
        var checks = await Run(A.ScopingPacket().With(name, text).Build());

        var failure = Assert.Single(Failures(checks, ScopingRules.WellFormed));
        Assert.Equal($"file {name}", failure.Subject);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_byte_order_mark_or_invalid_utf8_fails_PKT_SCP_003()
    {
        var checks = await Run(A.ScopingPacket().With("challenge.json", [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}']).With("brief.md", [0xFF, 0xFE, 0x41]).Build());

        Assert.Equal(["file brief.md", "file challenge.json"], Failures(checks, ScopingRules.WellFormed).Select(static c => c.Subject), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Blank_lines_in_a_jsonl_file_are_allowed()
    {
        var checks = await Run(A.ScopingPacket().With("answers.jsonl", "\n{\"a\":1}\n\n").Build());

        Assert.Empty(Failures(checks, ScopingRules.WellFormed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_malformed_lenses_json_fails_PKT_SCP_003_and_leaves_PKT_SCP_004_and_005_unchecked()
    {
        var checks = await Run(A.ScopingPacket().With("lenses.json", "{").Build());

        _ = Assert.Single(Failures(checks, ScopingRules.WellFormed));
        Assert.All(checks.Where(static c => IsLensRule(c)), static c => Assert.Equal(CheckState.Unchecked, c.State));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 2,")]
    [InlineData("\"depth\": 2, \"hops\": [], \"mapVersion\": \"v13\", \"foundCount\": 2", "\"depth\": 99, \"hops\": [], \"mapVersion\": \"v13\", \"foundCount\": 2")]
    [InlineData("\"hops\": [\"state\", \"dependency\", \"flow\"]", "\"hops\": [\"sibling\"]")]
    [InlineData("\"hops\": [\"state\", \"dependency\", \"flow\"]", "\"hops\": 2")]
    [InlineData("\"hops\": [\"state\", \"dependency\", \"flow\"]", "\"hops\": [\"state\", \"state\"]")]
    [InlineData("\"mapVersion\": \"v13\", \"foundCount\": 2", "\"mapVersion\": \"\", \"foundCount\": 2")]
    [InlineData("\"mapVersion\": \"v13\", \"foundCount\": 2", "\"mapVersion\": 13, \"foundCount\": 2")]
    [InlineData("\"foundCount\": 2", "\"foundCount\": 1")]
    [InlineData("\"band\": \"code\"", "\"band\": \"sky\"")]
    [InlineData("\"lensItem\": \"L-code-1\", \"trust\": \"untrusted\"", "\"lensItem\": \"L-code-1\", \"trust\": \"trusted\"")]
    [InlineData("\"lensItem\": \"L-code-1\"", "\"lensItem\": \"code-1\"")]
    [InlineData("\"lensItem\": \"L-code-1\"", "\"lensItem\": \"L-code-0\"")]
    [InlineData("\"from\": \"code:src/a.cs\"", "\"from\": \"\"")]
    [InlineData("\"truncated\": []", "\"truncated\": [\"code\", \"code\"]")]
    [InlineData("\"confirmed\": false,", "")]
    [InlineData("\"kind\": \"file\",", "\"kind\": \"file\", \"extra\": 1,")]
    public async Task A_lenses_json_off_its_schema_fails_PKT_SCP_004(string from, string to)
    {
        var checks = await Run(A.ScopingPacket().EditLenses(from, to).Build());

        Assert.NotEmpty(Failures(checks, ScopingRules.LensesSchema));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"hops\": [\"state\", \"dependency\", \"flow\"]", "\"hops\": [\"flow\", \"component\", \"state\"]")]
    [InlineData("\"hops\": [\"state\", \"dependency\", \"flow\"]", "\"hops\": []")]
    public async Task Hops_are_any_unique_list_of_the_four_kinds_in_any_order_and_may_be_empty(string from, string to)
    {
        var checks = await Run(A.ScopingPacket().EditLenses(from, to).Build());

        Assert.All(checks, static c => Assert.Equal(CheckState.Pass, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_that_is_missing_fails_PKT_SCP_004()
    {
        var text = ScopingPacketBuilder.GoldLenses.Replace("\"history\":", "\"histories\":", StringComparison.Ordinal);

        var checks = await Run(A.ScopingPacket().With("lenses.json", text).Build());

        Assert.NotEmpty(Failures(checks, ScopingRules.LensesSchema));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_id_in_the_wrong_lens_or_used_twice_fails_PKT_SCP_005()
    {
        var wrongLens = await Run(A.ScopingPacket().EditLenses("\"lensItem\": \"L-code-2\"", "\"lensItem\": \"L-product-2\"").Build());
        var twice = await Run(A.ScopingPacket().EditLenses("\"lensItem\": \"L-code-2\"", "\"lensItem\": \"L-code-1\"").Build());

        Assert.Contains("names the product lens", Assert.Single(Failures(wrongLens, ScopingRules.LensIds)).Message, StringComparison.Ordinal);
        Assert.Contains("more than once", Assert.Single(Failures(twice, ScopingRules.LensIds)).Message, StringComparison.Ordinal);
        Assert.Empty(Failures(wrongLens, ScopingRules.LensesSchema));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Truncated_bands_may_report_a_count_beyond_what_is_listed()
    {
        var checks = await Run(A.ScopingPacket()
            .EditLenses("\"foundCount\": 2", "\"foundCount\": 140")
            .EditLenses("\"truncated\": []", "\"truncated\": [\"code\"]")
            .Build());

        Assert.All(checks, static c => Assert.Equal(CheckState.Pass, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_rule_ids_are_not_the_build_kinds_write_scope_rules()
    {
        var ids = new[] { ScopingRules.FilesPresent, ScopingRules.NoUnlisted, ScopingRules.WellFormed, ScopingRules.LensesSchema, ScopingRules.LensIds };

        Assert.All(ids, static id => Assert.StartsWith("PKT-SCP-", id, StringComparison.Ordinal));
        Assert.DoesNotContain(PacketRules.ScopeExists, ids, StringComparer.Ordinal);
        Assert.DoesNotContain(PacketRules.ScopeOverlap, ids, StringComparer.Ordinal);
    }
}
