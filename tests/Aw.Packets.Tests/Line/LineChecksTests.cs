using System.Globalization;
using System.Text;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Line;

public sealed class LineChecksTests
{
    public static TheoryData<string> Kinds => ["design", "plan", "phase", "proof", "review", "handoff"];

    internal static async Task<IReadOnlyList<CheckResult>> Run(PacketKind kind, Dictionary<string, byte[]> files, string? sha = null)
    {
        var inspector = new LinePacketInspector(
            new InMemoryLinePacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(sha ?? PacketBuilder.BaseSha, PacketBuilder.BaseFiles),
            PacketFiles.Json);
        var inspection = await inspector.Inspect(kind, "pkt", "repo", sizes: null, CancellationToken.None);
        return inspection.Value.Checks;
    }

    private static IEnumerable<CheckResult> Failures(IEnumerable<CheckResult> checks, string rule) =>
        checks.Where(c => c.Rule == rule && c.State == CheckState.Fail);

    private static bool Is(string value, params string[] any) => any.Contains(value, StringComparer.Ordinal);

    private static PacketKind Kind(string name) => PacketKinds.Find(name)!;

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task The_gold_packet_of_every_line_kind_passes_every_rule_and_names_them_with_the_kinds_prefix(string name)
    {
        var kind = Kind(name);

        var checks = await Run(kind, A.LinePacket(kind).Build());

        Assert.All(checks.Where(static c => !c.Rule.StartsWith("PKT-SIZE", StringComparison.Ordinal) && c.Rule != PacketRules.HoldoutSealed), static c => Assert.Equal(CheckState.Pass, c.State));
        var prefix = LineRules.For(kind)!.Prefix;
        Assert.Equal(
            Enumerable.Range(1, 6).Select(n => prefix + "-00" + n.ToString(CultureInfo.InvariantCulture)),
            checks.Where(c => c.Rule.StartsWith(prefix, StringComparison.Ordinal)).Select(static c => c.Rule).Take(6), StringComparer.Ordinal);
        Assert.Contains(checks, static c => c.Rule == PacketRules.BaseCommit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_kind_has_its_own_prefix_and_the_kind_specific_rules_follow_001_to_006()
    {
        var phase = await Run(Kind("phase"), A.LinePacket(PacketKinds.Phase).Build());
        var proof = await Run(Kind("proof"), A.LinePacket(PacketKinds.Proof).Build());
        var design = await Run(Kind("design"), A.LinePacket(PacketKinds.Design).Build());
        var handoff = await Run(Kind("handoff"), A.LinePacket(PacketKinds.Handoff).Build());
        var plan = await Run(Kind("plan"), A.LinePacket(PacketKinds.Plan).Build());

        Assert.Equal(
            ["PKT-PHS-001", "PKT-PHS-002", "PKT-PHS-003", "PKT-PHS-004", "PKT-PHS-005", "PKT-PHS-006", "PKT-PHS-007", "PKT-PHS-008", "PKT-PHS-009", "PKT-PHS-010", "PKT-PHS-011", "PKT-PHS-012", "PKT-HOLD-003", "PKT-BASE-001"],
            phase.Select(static c => c.Rule), StringComparer.Ordinal);
        Assert.Equal(["PKT-PRF-007", "PKT-PRF-008"], proof.Select(static c => c.Rule).Where(static r => Is(r, "PKT-PRF-007", "PKT-PRF-008")), StringComparer.Ordinal);
        Assert.Equal(["PKT-DSN-007", "PKT-DSN-008"], design.Select(static c => c.Rule).Where(static r => Is(r, "PKT-DSN-007", "PKT-DSN-008")), StringComparer.Ordinal);
        Assert.Contains(handoff, static c => c.Rule == "PKT-HND-007");
        Assert.Contains(plan, static c => c.Rule == "PKT-PLN-007");
        Assert.Contains(plan, static c => c.Rule == PacketRules.Acyclic);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task A_missing_file_fails_001_and_an_unlisted_entry_fails_002(string name)
    {
        var kind = Kind(name);
        var prefix = LineRules.For(kind)!.Prefix;

        var missing = await Run(kind, A.LinePacket(kind).Without("index.json").Build());
        var unlisted = await Run(kind, A.LinePacket(kind).With("notes.txt", "x").With("attachments/a.png", "x").Build());

        Assert.Equal(["file index.json"], Failures(missing, $"{prefix}-001").Select(static c => c.Subject), StringComparer.Ordinal);
        Assert.Equal(CheckState.Unchecked, missing.Single(c => c.Rule == $"{prefix}-006").State);
        Assert.Equal(["entry attachments/", "entry notes.txt"], Failures(unlisted, $"{prefix}-002").Select(static c => c.Subject), StringComparer.Ordinal);
        Assert.All(Failures(unlisted, $"{prefix}-002"), static f => Assert.False(string.IsNullOrEmpty(f.Fix)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("design", "sections.json", "{")]
    [InlineData("design", "coverage.json", "[]")]
    [InlineData("design", "lens-rounds.jsonl", "{\"a\":1}\nnot json\n")]
    [InlineData("plan", "answers.jsonl", "[1]\n")]
    [InlineData("phase", "signals.json", "{\"a\":1,\"a\":2}")]
    [InlineData("proof", "red.json", "{\"a\":[null]}")]
    [InlineData("review", "review.json", "{")]
    [InlineData("handoff", "green.json", "[]")]
    public async Task A_file_that_is_not_the_shape_its_format_names_fails_003(string name, string file, string text)
    {
        var kind = Kind(name);

        var checks = await Run(kind, A.LinePacket(kind).With(file, text).Build());

        Assert.Equal([$"file {file}"], Failures(checks, $"{LineRules.For(kind)!.Prefix}-003").Select(static c => c.Subject), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_text_file_that_is_not_utf8_fails_003()
    {
        var checks = await Run(PacketKinds.Design, A.LinePacket(PacketKinds.Design).With("spec.md", [0xFF, 0xFE, 0x41]).Build());

        _ = Assert.Single(Failures(checks, "PKT-DSN-003"));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task A_manifest_that_says_another_kind_or_has_an_unknown_field_or_is_missing_a_link_fails_004(string name)
    {
        var kind = Kind(name);
        var rule = $"{LineRules.For(kind)!.Prefix}-004";

        var wrongKind = await Run(kind, A.LinePacket(kind).Edit("packet.json", $"\"kind\": \"{name}\"", "\"kind\": \"build\"").Build());
        var unknown = await Run(kind, A.LinePacket(kind).Edit("packet.json", "\"status\"", "\"owner\": \"x\", \"status\"").Build());
        var noLink = await Run(kind, A.LinePacket(kind).Edit("packet.json", "\"index\": \"index.json\"", "\"x\": \"index.json\"").Build());
        var shortSchema = await Run(kind, A.LinePacket(kind).Edit("packet.json", "\"schemaVersion\": 1", "\"schemaVersion\": 2").Build());

        Assert.Contains("kind is 'build'", Assert.Single(Failures(wrongKind, rule)).Message, StringComparison.Ordinal);
        _ = Assert.Single(Failures(unknown, rule));
        _ = Assert.Single(Failures(noLink, rule));
        Assert.Contains("schemaVersion is 2", Assert.Single(Failures(shortSchema, rule)).Message, StringComparison.Ordinal);
        Assert.Equal(CheckState.Unchecked, noLink.Single(c => c.Rule == $"{LineRules.For(kind)!.Prefix}-005").State);
        Assert.Equal(CheckState.Unchecked, unknown.Single(static c => c.Rule == PacketRules.BaseCommit).State);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task A_link_that_names_another_file_fails_005(string name)
    {
        var kind = Kind(name);
        var first = kind.Files.First(static f => string.Equals(f.Link, "index", StringComparison.Ordinal));

        var checks = await Run(kind, A.LinePacket(kind).Edit("packet.json", "\"index\": \"index.json\"", "\"index\": \"other.json\"").Build());

        Assert.Contains("links.index", Assert.Single(Failures(checks, $"{LineRules.For(kind)!.Prefix}-005")).Message, StringComparison.Ordinal);
        Assert.Equal("index.json", first.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proof_file_the_manifest_does_not_list_fails_005_and_so_does_a_listed_file_that_is_not_there()
    {
        var unlinked = await Run(PacketKinds.Proof, A.LinePacket(PacketKinds.Proof).With("evidence/extra.png", "png").Build());
        var phantom = await Run(PacketKinds.Proof, A.LinePacket(PacketKinds.Proof).Edit("packet.json", "evidence/shot.txt", "evidence/gone.txt").Build());
        var twice = await Run(PacketKinds.Proof, A.LinePacket(PacketKinds.Proof).Edit("packet.json", "\"app-run/run.txt\"", "\"app-run/run.txt\", \"app-run/run.txt\"").Build());

        Assert.Contains("evidence/extra.png", Assert.Single(Failures(unlinked, "PKT-PRF-005")).Message, StringComparison.Ordinal);
        Assert.Equal(2, Failures(phantom, "PKT-PRF-005").Count());
        Assert.Contains("twice", Assert.Single(Failures(twice, "PKT-PRF-005")).Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task An_index_with_another_kind_a_repeated_parent_a_missing_chain_or_an_unknown_field_fails_006(string name)
    {
        var kind = Kind(name);
        var rule = $"{LineRules.For(kind)!.Prefix}-006";
        var parents = $"[\"{LinePacketBuilder.Hash}\", \"{LinePacketBuilder.Hash}\"]";

        var wrongKind = await Run(kind, A.LinePacket(kind).Edit("index.json", $"\"kind\": \"{name}\"", "\"kind\": \"scoping\"").Build());
        var repeated = await Run(kind, A.LinePacket(kind).Edit("index.json", "\"parents\": []", $"\"parents\": {parents}").Build());
        var noChain = await Run(kind, A.LinePacket(kind).With("index.json", $"{{ \"schemaVersion\": 1, \"kind\": \"{name}\", \"parents\": [] }}").Build());
        var withId = await Run(kind, A.LinePacket(kind).Edit("index.json", "\"parents\"", "\"id\": \"x\", \"parents\"").Build());
        var badHash = await Run(kind, A.LinePacket(kind).Edit("index.json", "\"parents\": []", "\"parents\": [\"abc\"]").Build());

        Assert.Contains("kind is 'scoping'", Assert.Single(Failures(wrongKind, rule)).Message, StringComparison.Ordinal);
        Assert.Contains("twice", Assert.Single(Failures(repeated, rule)).Message, StringComparison.Ordinal);
        _ = Assert.Single(Failures(noChain, rule));
        _ = Assert.Single(Failures(withId, rule));
        _ = Assert.Single(Failures(badHash, rule));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_design_packet_can_extend_a_chain_and_its_acceptance_file_is_checked_for_its_shape_and_for_LC_2()
    {
        var kind = PacketKinds.Design;
        var passing = await Run(kind, A.LinePacket(kind).With("acceptance.json", LinePacketBuilder.Acceptance("passing")).Build());
        var twice = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"items\": [", "\"items\": [{ \"id\": \"AC-1\", \"text\": \"x\", \"status\": \"failing\", \"evidence\": [] },").Build());
        var noParent = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"parent\": \"" + LinePacketBuilder.OtherHash + "\", ", string.Empty).Build());
        var noText = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"text\": \"The link expires.\"", "\"text\": \" \"").Build());
        var badStatus = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"failing\"", "\"done\"").Build());
        var extra = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"form\": \"event\"", "\"form\": \"event\", \"citations\": [\"S1\"], \"trust\": \"owner\"").Build());

        Assert.Contains("only the proof pack", Assert.Single(Failures(passing, "PKT-DSN-008")).Message, StringComparison.Ordinal);
        Assert.Contains("appears 2 times", Assert.Single(Failures(twice, "PKT-DSN-008")).Message, StringComparison.Ordinal);
        _ = Assert.Single(Failures(noParent, "PKT-DSN-007"));
        Assert.Contains("no text", Assert.Single(Failures(noText, "PKT-DSN-007")).Message, StringComparison.Ordinal);
        _ = Assert.Single(Failures(badStatus, "PKT-DSN-007"));
        Assert.Empty(Failures(extra, "PKT-DSN-007"));
        Assert.Equal(CheckState.Unchecked, noParent.Single(static c => c.Rule == "PKT-DSN-008").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proof_pack_may_flip_an_item_to_passing_only_with_evidence()
    {
        var kind = PacketKinds.Proof;
        var bad = await Run(kind, A.LinePacket(kind).Edit("acceptance.json", "\"commit\": \"" + PacketBuilder.BaseSha + "\"", "\"commit\": \"abc\"").Build());
        var noEvidence = await Run(kind, A.LinePacket(kind).With("acceptance.json", "{ \"schemaVersion\": 1, \"parent\": \"" + LinePacketBuilder.OtherHash + "\", \"items\": [{ \"id\": \"AC-1\", \"text\": \"t\", \"status\": \"passing\", \"evidence\": [] }] }").Build());

        _ = Assert.Single(Failures(noEvidence, "PKT-PRF-008"));
        Assert.Contains("no evidence", Failures(noEvidence, "PKT-PRF-008").Single().Message, StringComparison.Ordinal);
        _ = Assert.Single(Failures(bad, "PKT-PRF-007"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_packet_id_is_line_phase_with_an_optional_amendment_suffix()
    {
        foreach (var id in new[] { "expiry-p1", "expiry-p1-a1", "line-x-p2-a12", "a-b" })
        {
            var checks = await Run(PacketKinds.Phase, A.LinePacket(PacketKinds.Phase, id).Edit("phase.json", "\"deps\": []", id.EndsWith("-a1", StringComparison.Ordinal) || id.EndsWith("-a12", StringComparison.Ordinal)
                ? "\"deps\": [], \"amends\": { \"parent\": \"" + LinePacketBuilder.Hash + "\", \"paths\": [\"src/c.cs\"], \"reason\": \"needed\", \"at\": \"2026-10-16T09:00:00Z\" }"
                : "\"deps\": []").Build());

            Assert.Equal(CheckState.Pass, checks.Single(static c => c.Rule == "PKT-PHS-007").State);
            Assert.Equal(CheckState.Pass, checks.Single(static c => c.Rule == "PKT-PHS-009").State);
        }

        foreach (var id in new[] { "expiry", "expiry-p1-a0", "expiry-p1-a" })
        {
            var checks = await Run(PacketKinds.Phase, A.LinePacket(PacketKinds.Phase, id).Build());

            Assert.Equal(id == "expiry" ? CheckState.Fail : CheckState.Pass, checks.Single(static c => c.Rule == "PKT-PHS-007").State);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_amendment_suffix_and_the_amends_object_come_together()
    {
        var suffixOnly = await Run(PacketKinds.Phase, A.LinePacket(PacketKinds.Phase, "expiry-p1-a1").Build());
        var objectOnly = await Run(PacketKinds.Phase, A.LinePacket(PacketKinds.Phase)
            .Edit("phase.json", "\"deps\": []", "\"deps\": [], \"amends\": { \"parent\": \"" + LinePacketBuilder.Hash + "\", \"paths\": [], \"reason\": \" \", \"at\": \"2026-10-16T09:00:00Z\" }").Build());
        var badPath = await Run(PacketKinds.Phase, A.LinePacket(PacketKinds.Phase, "expiry-p1-a2")
            .Edit("phase.json", "\"deps\": []", "\"deps\": [], \"amends\": { \"parent\": \"" + LinePacketBuilder.Hash + "\", \"paths\": [\"new: /abs\", \"new: ../x\"], \"reason\": \"r\", \"at\": \"2026-10-16T09:00:00Z\" }").Build());

        Assert.Contains("no 'amends'", Assert.Single(Failures(suffixOnly, "PKT-PHS-009")).Message, StringComparison.Ordinal);
        Assert.Equal(3, Failures(objectOnly, "PKT-PHS-009").Count());
        Assert.Equal(2, Failures(badPath, "PKT-PHS-009").Count());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_json_that_breaks_its_schema_or_its_scope_syntax_fails_008()
    {
        var kind = PacketKinds.Phase;
        var unknown = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"goal\"", "\"owner\": \"x\", \"goal\"").Build());
        var badKind = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"feature\"", "\"epic\"").Build());
        var noScope = await Run(kind, A.LinePacket(kind).Edit("phase.json", "[\"src/**\", \"new: src/expiry.cs\"]", "[]").Build());
        var badGlob = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"src/**\"", "\"new: ../x\"").Build());
        var depBase = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"default-branch\", \"ref\": \"main\"", "\"dependency-head\", \"ref\": \"p2\"").Build());
        var okDepBase = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"deps\": []", "\"deps\": [\"p2\"]").Edit("phase.json", "\"default-branch\", \"ref\": \"main\"", "\"dependency-head\", \"ref\": \"p2\"").Build());
        var emptyReason = await Run(kind, A.LinePacket(kind).Edit("phase.json", "createLink gains an argument", " ").Build());
        var twice = await Run(kind, A.LinePacket(kind).Edit("phase.json", "\"deps\": []", "\"deps\": [\"p2\", \"p2\"]").Build());

        _ = Assert.Single(Failures(unknown, "PKT-PHS-008"));
        _ = Assert.Single(Failures(badKind, "PKT-PHS-008"));
        Assert.Contains("allowedFiles is empty", Assert.Single(Failures(noScope, "PKT-PHS-008")).Message, StringComparison.Ordinal);
        Assert.Contains("new: ../x", Assert.Single(Failures(badGlob, "PKT-PHS-008")).Message, StringComparison.Ordinal);
        Assert.Contains("not one of deps", Assert.Single(Failures(depBase, "PKT-PHS-008")).Message, StringComparison.Ordinal);
        Assert.Empty(Failures(okDepBase, "PKT-PHS-008"));
        _ = Assert.Single(Failures(emptyReason, "PKT-PHS-008"));
        Assert.Contains("twice", Assert.Single(Failures(twice, "PKT-PHS-008")).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_packet_checks_its_acceptance_chain_and_its_holdout_summary()
    {
        var kind = PacketKinds.Phase;
        var passing = await Run(kind, A.LinePacket(kind).With("acceptance.json", LinePacketBuilder.Acceptance("passing")).Build());
        var noHash = await Run(kind, A.LinePacket(kind).Edit("holdout.json", LinePacketBuilder.Hash, "abc").Build());
        var withText = await Run(kind, A.LinePacket(kind).Edit("holdout.json", "\"count\"", "\"scenarios\": [], \"count\"").Build());
        var negative = await Run(kind, A.LinePacket(kind).Edit("holdout.json", "\"count\": 3", "\"count\": -1").Build());

        _ = Assert.Single(Failures(passing, "PKT-PHS-011"));
        _ = Assert.Single(Failures(noHash, "PKT-PHS-012"));
        _ = Assert.Single(Failures(withText, "PKT-PHS-012"));
        _ = Assert.Single(Failures(negative, "PKT-PHS-012"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hand_off_checks_pr_json()
    {
        var kind = PacketKinds.Handoff;
        var shortSha = await Run(kind, A.LinePacket(kind).Edit("pr.json", PacketBuilder.BaseSha, "abc123").Build());
        var noStack = await Run(kind, A.LinePacket(kind).Edit("pr.json", "\"stackParent\": null, ", string.Empty).Build());
        var stacked = await Run(kind, A.LinePacket(kind).Edit("pr.json", "\"stackParent\": null", "\"stackParent\": \"sindri/expiry/p0\"").Build());
        var blank = await Run(kind, A.LinePacket(kind).Edit("pr.json", "\"title\": \"feat: expiry\"", "\"title\": \" \"").Build());
        var labels = await Run(kind, A.LinePacket(kind).Edit("pr.json", "[\"area:sharing\"]", "[\"a\", \"a\"]").Build());

        _ = Assert.Single(Failures(shortSha, "PKT-HND-007"));
        _ = Assert.Single(Failures(noStack, "PKT-HND-007"));
        Assert.Empty(Failures(stacked, "PKT-HND-007"));
        _ = Assert.Single(Failures(blank, "PKT-HND-007"));
        _ = Assert.Single(Failures(labels, "PKT-HND-007"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plan_graph_gets_the_build_kinds_task_rules_over_tasks_json()
    {
        var kind = PacketKinds.Plan;
        var tasks = new TaskBuilder("P1").Writing("src/a.cs").Example("P1-e1", holdout: false, "test").Example("P1-h1", holdout: true, "api-transcript");
        var overlap = A.Packet().With(tasks).With(new TaskBuilder("P2").Writing("src/a.cs").Example("P2-e1", holdout: false, "test").Example("P2-h1", holdout: true, "api-transcript")).Build();
        var cycle = A.Packet().With(new TaskBuilder("P1").After("P2").Example("P1-e1", holdout: false, "test").Example("P1-h1", holdout: true, "api-transcript"))
            .With(new TaskBuilder("P2").After("P1").Writing("docs/guide.md").Example("P2-e1", holdout: false, "test").Example("P2-h1", holdout: true, "api-transcript")).Build();
        var held = A.Packet().With(new TaskBuilder("P1").ExampleWithText("P1-h1", holdout: true, "the hidden text").Example("P1-e1", holdout: false, "test")).Build();

        var overlapChecks = await Run(kind, A.LinePacket(kind).With("tasks.json", Kernel.Json.AwJson.Serialize(overlap.Tasks, PacketFiles.Json)).Build());
        var cycleChecks = await Run(kind, A.LinePacket(kind).With("tasks.json", Kernel.Json.AwJson.Serialize(cycle.Tasks, PacketFiles.Json)).Build());
        var heldChecks = await Run(kind, A.LinePacket(kind).With("tasks.json", Kernel.Json.AwJson.Serialize(held.Tasks, PacketFiles.Json)).Build());

        _ = Assert.Single(Failures(overlapChecks, PacketRules.ScopeOverlap));
        _ = Assert.Single(Failures(cycleChecks, PacketRules.Acyclic));
        _ = Assert.Single(Failures(heldChecks, PacketRules.HoldoutStored));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tasks_json_that_is_not_a_task_plan_fails_PLN_007_and_leaves_the_task_rules_unchecked()
    {
        var kind = PacketKinds.Plan;

        var unknown = await Run(kind, A.LinePacket(kind).With("tasks.json", "{ \"schemaVersion\": 1, \"workstreams\": [], \"tasks\": [], \"extra\": 1 }").Build());
        var version = await Run(kind, A.LinePacket(kind).With("tasks.json", "{ \"schemaVersion\": 2, \"workstreams\": [], \"tasks\": [] }").Build());

        _ = Assert.Single(Failures(unknown, "PKT-PLN-007"));
        Assert.Contains("schemaVersion is 2", Assert.Single(Failures(version, "PKT-PLN-007")).Message, StringComparison.Ordinal);
        Assert.All(unknown.Where(static c => Is(c.Rule, "PKT-ID-001", "PKT-GRAPH-002", "PKT-SCOPE-002", "PKT-HOLD-002")), static c => Assert.Equal(CheckState.Unchecked, c.State));
        Assert.Contains(unknown, static c => c.Rule == PacketRules.BaseCommit && c.State == CheckState.Pass);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public async Task PKT_BASE_001_applies_to_every_kind(string name)
    {
        var kind = Kind(name);
        var unknown = await Run(kind, A.LinePacket(kind).Edit("packet.json", PacketBuilder.BaseSha, "0000000000000000000000000000000000000001").Build());
        var shortSha = await Run(kind, A.LinePacket(kind).Edit("packet.json", PacketBuilder.BaseSha, "abc123").Build());
        var unreadable = await Run(kind, A.LinePacket(kind).With("packet.json", "{").Build());

        Assert.Contains("isn't in the repository", Assert.Single(Failures(unknown, PacketRules.BaseCommit)).Message, StringComparison.Ordinal);
        Assert.Contains("isn't a full commit sha", Assert.Single(Failures(shortSha, PacketRules.BaseCommit)).Message, StringComparison.Ordinal);
        Assert.Equal(CheckState.Unchecked, unreadable.Single(static c => c.Rule == PacketRules.BaseCommit).State);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public void The_typed_links_name_exactly_the_files_and_directories_the_kind_lists(string name)
    {
        var kind = Kind(name);
        var expected = kind.Files.Where(static f => f.Link is not null).Select(static f => f.Link).OfType<string>().Concat(kind.Directories.Select(static d => d.Link)).Order(StringComparer.Ordinal);
        var actual = System.Text.Json.JsonDocument.Parse(A.LinePacket(kind).Build()["packet.json"]).RootElement.GetProperty("links").EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal);

        Assert.Equal(expected, actual, StringComparer.Ordinal);
        Assert.Equal(kind.Files.Count, kind.Files.Count(static f => f.Link is not null) + 1);   // every file but packet.json is linked
        Assert.True(Encoding.UTF8.GetString(A.LinePacket(kind).Build()["index.json"]).Contains(name, StringComparison.Ordinal));
    }
}
