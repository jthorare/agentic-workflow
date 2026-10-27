using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The one place status and doctor decide which launches are agent sessions that went idle with no report (issue #1265).</summary>
[Lane(TestLane.Unit)]
public sealed class StalledLaunchesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly InstanceAddress _instance = InstanceAddress.From(_item.Value + "/step/1");

    private static LaunchRow Row(int n, SessionEndReason? ended, TimeSpan requestedAgo, InstanceAddress? instance, TimeSpan? endedAgo = null) =>
        new(LaunchId.From("LCH-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), _item, Repo: null, LaunchMode.Open, AgentId.From("claude-code"), "service:agent", LaunchState.Started,
            _now - requestedAgo, OutcomeAt: null, Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null,
            ended is { } reason ? new LaunchEnd(reason, ExitCode: null, _now - (endedAgo ?? TimeSpan.FromMinutes(1))) : null, new LaunchStep(instance, "draft", Attempt: n), Sequence: n);

    [Fact]
    public void A_launch_that_ended_idle_for_a_step_counts()
    {
        var found = StalledLaunches.Find([Row(1, SessionEndReason.Idle, TimeSpan.FromMinutes(10), _instance)], _now);

        Assert.Equal(LaunchId.From("LCH-" + 1.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), Assert.Single(found).Launch);
    }

    [Theory]
    [InlineData(SessionEndReason.Exit)]
    [InlineData(SessionEndReason.Lost)]
    public void Another_end_does_not_count(SessionEndReason reason) => Assert.Empty(StalledLaunches.Find([Row(1, reason, TimeSpan.FromMinutes(10), _instance)], _now));

    [Fact]
    public void A_live_launch_does_not_count() => Assert.Empty(StalledLaunches.Find([Row(1, ended: null, TimeSpan.FromMinutes(10), _instance)], _now));

    [Fact]
    public void An_attended_launch_with_no_step_does_not_count() => Assert.Empty(StalledLaunches.Find([Row(1, SessionEndReason.Idle, TimeSpan.FromMinutes(10), instance: null)], _now));

    [Fact]
    public void A_newer_launch_of_the_same_instance_means_the_engine_retried_so_it_does_not_count() =>
        Assert.Empty(StalledLaunches.Find([Row(1, SessionEndReason.Idle, TimeSpan.FromMinutes(10), _instance), Row(2, ended: null, TimeSpan.FromMinutes(9), _instance)], _now));

    [Fact]
    public void A_newer_launch_of_another_instance_does_not_hide_it() =>
        Assert.Equal(_instance, Assert.Single(StalledLaunches.Find([Row(1, SessionEndReason.Idle, TimeSpan.FromMinutes(10), _instance), Row(2, ended: null, TimeSpan.FromMinutes(9), InstanceAddress.From(_item.Value + "/step/2"))], _now)).Instance);

    [Fact]
    public void One_that_ended_past_the_window_stops_counting_but_counts_without_a_clock()
    {
        var old = Row(1, SessionEndReason.Idle, TimeSpan.FromDays(3), _instance, endedAgo: TimeSpan.FromDays(2));

        Assert.Empty(StalledLaunches.Find([old], _now));
        Assert.Equal(_instance, Assert.Single(StalledLaunches.Find([old], now: null)).Instance);
    }

    private static StalledLaunch Stalled(InstanceAddress? instance = null) => Assert.Single(StalledLaunches.Find([Row(1, SessionEndReason.Idle, TimeSpan.FromMinutes(10), instance ?? _instance)], _now));

    private static EngineItemPart Part(string address, string state = "draft", bool ended = false, bool cancelled = false) =>
        new(address, state, RunOpen: true, StepRuntime.Agent, Suspension: null, ended, cancelled);

    [Fact]
    public void An_item_with_no_engine_part_keeps_the_launch() => Assert.False(StalledLaunches.MovedPast(Stalled(), engine: null));

    [Fact]
    public void An_instance_still_in_the_launchs_step_keeps_it() => Assert.False(StalledLaunches.MovedPast(Stalled(), Part(_instance.Value)));

    [Fact]
    public void An_instance_that_ended_drops_it() => Assert.True(StalledLaunches.MovedPast(Stalled(), Part(_instance.Value, ended: true)));

    [Fact]
    public void An_instance_that_was_cancelled_drops_it() => Assert.True(StalledLaunches.MovedPast(Stalled(), Part(_instance.Value, ended: true, cancelled: true)));

    [Fact]
    public void An_instance_in_another_state_drops_it() => Assert.True(StalledLaunches.MovedPast(Stalled(), Part(_instance.Value, state: "review")));

    [Fact]
    public void A_root_that_is_no_longer_one_of_the_items_live_roots_drops_it() => Assert.True(StalledLaunches.MovedPast(Stalled(), Part(_item.Value + "/other/1")));

    [Fact]
    public void A_child_of_an_ended_root_drops_it() =>
        Assert.True(StalledLaunches.MovedPast(Stalled(InstanceAddress.From(_item.Value + "/step/1/child.1")), Part(_instance.Value, ended: true)));

    [Fact]
    public void A_child_of_a_live_root_is_kept_whatever_state_the_root_is_in() =>
        Assert.False(StalledLaunches.MovedPast(Stalled(InstanceAddress.From(_item.Value + "/step/1/child.1")), Part(_instance.Value, state: "review")));
}
