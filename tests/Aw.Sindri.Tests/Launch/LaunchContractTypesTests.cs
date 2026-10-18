using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The launch events, their catalog rows and the launch error codes (API P10a, unit U0).</summary>
public sealed class LaunchContractTypesTests
{
    private static readonly string[] _launchEvents =
    [
        EventTypes.ItemOpenRequested, EventTypes.LaunchStarted, EventTypes.LaunchFailed, EventTypes.LaunchProbed,
        EventTypes.SessionEnded, EventTypes.SessionUsage, EventTypes.LaunchSessionReported, EventTypes.LaunchViewerAttached,
    ];

    private static readonly string[] _launchNumbers = ["001", "002", "003", "004", "006", "007", "008", "009", "010", "011", "012", "013"];

    [Fact, Trait("Lane", "unit")]
    public void Every_new_launch_event_invalidates_a_view()
    {
        var rows = EventCatalog.All.Where(e => _launchEvents.Contains(e.Type, StringComparer.Ordinal)).ToList();

        Assert.Equal(_launchEvents.Length, rows.Count);
        Assert.All(rows.Where(r => r.Public), r => Assert.All(r.Invalidates, v => Assert.Contains(v, ApiViews.All, StringComparer.Ordinal)));
        Assert.All(rows.Where(r => r.Public), r => Assert.NotEmpty(r.Invalidates));
    }

    [Fact, Trait("Lane", "unit")]
    public void Session_usage_is_the_only_private_launch_event()
    {
        var privateRows = EventCatalog.All.Where(e => _launchEvents.Contains(e.Type, StringComparer.Ordinal) && !e.Public).Select(e => e.Type).ToList();

        Assert.Equal([EventTypes.SessionUsage], privateRows);
    }

    [Fact, Trait("Lane", "unit")]
    public void LaunchStarted_public_form_has_no_viewer_detail()
    {
        var started = new LaunchStarted(
            A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId.From("0192f0c1-a2b3"), "sindri ITM-1",
            new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Failed, "SND-LAUNCH-001", "open: exit 3 (private stderr)"),
            ["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-x"], CatalogSource.Builtin, "hash");
        var h = new SliceHarness();
        var entry = h.Events.Entry(EventTypes.LaunchStarted, started, "launch.outcome:x", A.FirstItemId.Value);
        var row = new Core.Ledger.LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        var viewer = shown.Payload.GetProperty("viewer");
        Assert.Equal("SND-LAUNCH-001", viewer.GetProperty("code").GetString());
        Assert.False(viewer.TryGetProperty("detail", out _));
        Assert.DoesNotContain("private stderr", shown.Payload.GetRawText(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Launch_codes_are_unique_and_in_All()
    {
        var codes = _launchNumbers.Select(n => "SND-LAUNCH-" + n).ToList();

        var present = SindriErrors.All.Select(e => e.Code).ToList();

        Assert.All(codes, c => Assert.Contains(c, present, StringComparer.Ordinal));
        Assert.DoesNotContain("SND-LAUNCH-005", present, StringComparer.Ordinal);
        Assert.Contains("SND-AUTH-003", present, StringComparer.Ordinal);
        Assert.Equal(present.Count, present.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_launch_codes_pass_the_catalogue_check() => Assert.True(ErrorCatalogue.Validate(SindriErrors.All).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void A_launch_request_names_the_engines_launch_id()
    {
        var request = A.LaunchRequest().Build();

        _ = Assert.IsType<LaunchId>(request.Launch);
        Assert.True(request.Attended);
        Assert.Null(request.Step);
    }

    [Fact, Trait("Lane", "unit")]
    public void StepPolicy_is_target_neutral_with_the_three_modes_and_no_bypass()
    {
        var policy = new StepPolicy(["Read", "Grep"], StepPermissionMode.ReadOnly, WritablePath: null);
        var step = new StepArgs(Model: null, policy, new StepRef("build", "agent"));

        Assert.Equal(["Read", "Grep"], step.Policy!.AllowedTools);
        Assert.Equal(["ReadOnly", "Plan", "Default"], Enum.GetNames<StepPermissionMode>());
        Assert.Null(step.Policy.WritablePath);
    }
}
