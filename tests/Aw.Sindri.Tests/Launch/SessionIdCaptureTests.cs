using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The file fallback that finds a reported session id (launcher spec §4): exactly one candidate is used; more than one is refused as ambiguous.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionIdCaptureTests
{
    private const string One = "0a1b2c3d-0000-4000-8000-000000000001";
    private const string Two = "0a1b2c3d-0000-4000-8000-000000000002";

    private static readonly CaptureRule _codex = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        "/home/u/.codex/sessions/*/*/*/rollout-*.jsonl",
        "rollout-.*-([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\\.jsonl$");

    private static readonly IReadOnlySet<string> _none = new HashSet<string>(StringComparer.Ordinal);

    private static string Rollout(string id) => $"/home/u/.codex/sessions/2026/10/17/rollout-2026-10-17T10-00-00-{id}.jsonl";

    [Fact]
    public void One_candidate_is_the_id()
    {
        var scan = SessionIdCapture.Choose(_codex, [Rollout(One)], _none);

        Expect.Equal((One, false), (scan.Id!.Value.Value, scan.Ambiguous));
    }

    [Fact]
    public void More_than_one_candidate_is_ambiguous_and_uses_none()
    {
        var scan = SessionIdCapture.Choose(_codex, [Rollout(One), Rollout(Two)], _none);

        Expect.Equal((null, true), (scan.Id?.Value, scan.Ambiguous));
    }

    [Fact]
    public void The_same_id_in_two_files_is_one_candidate()
    {
        var scan = SessionIdCapture.Choose(_codex, [Rollout(One), $"/home/u/.codex/sessions/2026/10/18/rollout-2026-10-18T10-00-00-{One}.jsonl"], _none);

        Expect.Equal((One, false), (scan.Id!.Value.Value, scan.Ambiguous));
    }

    [Fact]
    public void An_id_another_launch_holds_is_not_a_candidate()
    {
        var scan = SessionIdCapture.Choose(_codex, [Rollout(One), Rollout(Two)], new HashSet<string>([One], StringComparer.Ordinal));

        Expect.Equal((Two, false), (scan.Id!.Value.Value, scan.Ambiguous));
    }

    [Fact]
    public void A_path_the_capture_does_not_match_or_an_id_the_pattern_refuses_is_ignored()
    {
        var scan = SessionIdCapture.Choose(_codex, ["/home/u/.codex/sessions/2026/10/17/notes.jsonl", "/home/u/.codex/sessions/rollout-x-not-a-uuid.jsonl"], _none);

        Expect.Equal((null, false), (scan.Id?.Value, scan.Ambiguous));
    }

    [Fact]
    public void A_rule_without_a_capture_finds_nothing()
    {
        var scan = SessionIdCapture.Choose(_codex with { Capture = null }, [Rollout(One)], _none);

        Expect.Equal((null, false), (scan.Id?.Value, scan.Ambiguous));
    }
}
