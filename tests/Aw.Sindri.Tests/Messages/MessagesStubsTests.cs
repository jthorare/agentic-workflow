using System.Reflection;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1s (messages spec §14): every first red test the phase table names for M1b to M1h exists under its own name, in the project and lane this table gives,
/// and the table matches the spec's. While a named test's stub throws NotSupportedException it reports SKIPPED through <see cref="RedUntil"/> (main stays
/// green); the day its phase builds the stub it runs for real with no edit. This test stays green throughout, and fails when a named test is renamed,
/// dropped or moved, or when §14 names a test nobody wrote.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class MessagesStubsTests
{
    private const string SpecPath = "docs/superpowers/specs/2026-11-12-sindri-messages-page-design.md";
    private const string RedactionFile = "tests/Aw.Redaction.Tests/SlackTokenShapeTests.cs";

    // (phase, test class in Aw.Sindri.Tests.Messages or the file of another project, test, lane)
    private static readonly (string Phase, string Owner, string Test, string Lane)[] _named =
    [
        ("M1b", nameof(MessageTriageTests), nameof(MessageTriageTests.MessageRank_orders_the_example), "unit"),
        ("M1b", nameof(MessageTriageTests), nameof(MessageTriageTests.Needs_reply_matches_the_one_definition), "unit"),
        ("M1b", nameof(MrkdwnTests), nameof(MrkdwnTests.Mrkdwn_round_trips_the_golden_set), "unit"),
        ("M1c", nameof(MessagesStoreTests), nameof(MessagesStoreTests.Needs_reply_rebuild_touches_only_the_changed_thread), "integration"),
        ("M1c", nameof(RetainMessagesTests), nameof(RetainMessagesTests.Retention_keeps_a_thread_in_needs_reply), "integration"),
        ("M1c", nameof(RetainMessagesTests), nameof(RetainMessagesTests.Retention_keeps_a_snoozed_thread_after_views_rebuild), "integration"),
        ("M1c", nameof(RetainMessagesTests), nameof(RetainMessagesTests.Retention_ends_a_skip_after_180_days), "integration"),
        ("M1d", nameof(SlackClientTests), nameof(SlackClientTests.Rate_limit_429_pauses_only_that_method), "unit"),
        ("M1d", nameof(SlackClientTests), nameof(SlackClientTests.Base_url_override_is_ignored_without_the_marker), "unit"),
        ("M1d", nameof(SlackClientTests), nameof(SlackClientTests.Connection_test_calls_auth_test), "unit"),
        ("M1d", RedactionFile, "Redaction_scrubs_xapp_and_xoxc", "unit"),
        ("M1d", nameof(ConnectMessagesTests), nameof(ConnectMessagesTests.Wrong_token_prefix_is_refused_before_any_call), "unit"),
        ("M1e", nameof(SyncMessagesTests), nameof(SyncMessagesTests.Gap_without_marker_fails), "integration"),
        ("M1e", nameof(SyncMessagesTests), nameof(SyncMessagesTests.Message_appears_within_30s_on_the_poll_path), "integration"),
        ("M1e", nameof(SyncMessagesTests), nameof(SyncMessagesTests.Reply_in_an_old_thread_is_seen), "integration"),
        ("M1e", nameof(SyncMessagesTests), nameof(SyncMessagesTests.Cold_start_fill_gives_a_sample), "integration"),
        ("M1f", nameof(MessagesReadRouteTests), nameof(MessagesReadRouteTests.ApiGoldens_messages_lists), "integration"),
        ("M1f", nameof(MessagesReadRouteTests), nameof(MessagesReadRouteTests.Session_read_returns_no_text), "integration"),
        ("M1g", nameof(ResolveLinksTests), nameof(ResolveLinksTests.Resolve_github_pr_url_to_row_key), "unit"),
        ("M1h", nameof(DoneAndReadPointerTests), nameof(DoneAndReadPointerTests.Done_leaves_needs_reply_and_returns_on_a_new_dm), "unit"),
        ("M1h", nameof(DoneAndReadPointerTests), nameof(DoneAndReadPointerTests.E_in_unread_moves_the_pointer_and_writes_no_home_event), "integration"),
    ];

    private static readonly string[] _phases = ["M1b", "M1c", "M1d", "M1e", "M1f", "M1g", "M1h"];

    [Fact]
    public void Messages_stubs_compile_and_every_named_test_fails()
    {
        var root = RepoRoot();

        // the table is §14's: the same names, phase by phase
        var spec = SpecNames(File.ReadAllLines(Path.Combine(root, SpecPath)));
        Assert.Equal(spec.OrderBy(static n => n.Phase + " " + n.Test, StringComparer.Ordinal), _named.Select(static n => (n.Phase, n.Test)).OrderBy(static n => n.Phase + " " + n.Test, StringComparer.Ordinal));

        // each named test exists, is a test, and runs in its lane
        foreach (var (phase, owner, test, lane) in _named)
        {
            if (owner.EndsWith(".cs", StringComparison.Ordinal))
            {
                var text = File.ReadAllText(Path.Combine(root, owner));
                Assert.True(text.Contains($" {test}(", StringComparison.Ordinal) && text.Contains($"\"Lane\", \"{lane}\"", StringComparison.Ordinal), $"{phase}: {owner} has no {lane} test {test}");
                continue;
            }

            var type = typeof(MessagesStubsTests).Assembly.GetType($"{typeof(MessagesStubsTests).Namespace}.{owner}");
            var method = type?.GetMethod(test, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(method is not null, $"{phase}: {owner}.{test} is missing");
            Assert.True(method.GetCustomAttributes<FactAttribute>().Any(), $"{phase}: {owner}.{test} is not a [Fact] or [Theory]");
            Assert.True(string.Equals(lane, LaneOf(method), StringComparison.Ordinal), $"{phase}: {owner}.{test} runs in lane '{LaneOf(method)}', not '{lane}'");
        }
    }

    // The 'First red test' cell (the sixth column) of each phase row M1b to M1h: every `Name` in it.
    private static IEnumerable<(string Phase, string Test)> SpecNames(IEnumerable<string> lines) =>
        lines.Select(static l => l.Split('|'))
            .Where(static cells => cells.Length > 6 && _phases.Contains(cells[1].Trim(), StringComparer.Ordinal))
            .SelectMany(static cells => TestName().Matches(cells[5]).Select(m => (cells[1].Trim(), m.Groups[1].Value)));

    private static string? LaneOf(MethodInfo method) =>
        LaneIn(method.CustomAttributes) ?? LaneIn(method.DeclaringType!.CustomAttributes);

    private static string? LaneIn(IEnumerable<CustomAttributeData> attributes) =>
        attributes.Select(static a => a.AttributeType.Name switch
        {
            "TraitAttribute" when a.ConstructorArguments is [{ Value: "Lane" }, { Value: string value }] => value,
            "LaneAttribute" when a.ConstructorArguments is [{ Value: int lane }] => ((TestLane)lane).ToString().ToLowerInvariant(),
            _ => null,
        }).FirstOrDefault(static l => l is not null);

    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(MessagesStubsTests).Assembly, "Messages"), "..", "..", ".."));

    [GeneratedRegex("`([A-Za-z][A-Za-z0-9_]*)`", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestName();
}
