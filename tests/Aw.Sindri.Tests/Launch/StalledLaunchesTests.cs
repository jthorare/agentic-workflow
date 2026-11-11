using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

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

    [Fact]
    public async Task The_read_looks_up_only_steps_that_ended_inside_the_window_and_answers_as_Find_does()
    {
        var rows = new List<LaunchRow>();
        for (var i = 0; i < 50; i++)
        {
            rows.Add(Row(i, SessionEndReason.Idle, TimeSpan.FromDays(3), InstanceAddress.From(_item.Value + "/old/" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)), endedAgo: TimeSpan.FromDays(2)));
        }

        rows.Add(Row(100, SessionEndReason.Idle, TimeSpan.FromHours(2), InstanceAddress.From(_item.Value + "/new/1"), endedAgo: TimeSpan.FromHours(1)));
        rows.Add(Row(101, SessionEndReason.Idle, TimeSpan.FromHours(2), InstanceAddress.From(_item.Value + "/new/2"), endedAgo: TimeSpan.FromHours(1)));
        var reader = new LaunchReader(rows);

        var read = (await StalledLaunches.Read(reader, _now, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(2, reader.InstanceLookups);
        Assert.Equal(StalledLaunches.Find(rows, _now).Select(static s => s.Launch), read.Select(static s => s.Launch));
        Assert.Equal(2, read.Count);
    }

    private sealed class LaunchReader(List<LaunchRow> rows) : Aw.Sindri.Core.Views.IViewReader
    {
        public int InstanceLookups { get; private set; }

        public ValueTask<Aw.Kernel.Result<IReadOnlyList<TRow>>> Where<TRow>(string view, Aw.Sindri.Core.Views.ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class
        {
            if (column == Aw.Sindri.Core.Views.ViewColumn.Instance)
            {
                InstanceLookups++;
            }

            IReadOnlyList<LaunchRow> found = column == Aw.Sindri.Core.Views.ViewColumn.Reason
                ? [.. rows.Where(static r => r.Ended?.Reason == SessionEndReason.Idle)]
                : [.. rows.Where(r => string.Equals(r.Step.Instance?.Value, value, StringComparison.Ordinal))];
            return ValueTask.FromResult(Aw.Kernel.Results.Success((IReadOnlyList<TRow>)(object)found));
        }

        public ValueTask<Aw.Kernel.Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Aw.Kernel.Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Aw.Kernel.Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Aw.Kernel.Result<Aw.Sindri.Core.Views.ProjectorState?>> State(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
