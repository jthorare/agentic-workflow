using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// A run row's time (#3305): under a day it keeps its age ("Ended 5m ago"); past a day it names the day and the clock time in local time ("Ended yesterday 07:00",
/// "Ended Mar 1 07:00", with the year when it is not this year), so every run from yesterday no longer reads "1d ago". The tip gives the exact moment.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunRowWhenTests
{
    private static readonly DateTimeOffset _now = At("2026-03-10T09:00:00Z");

    private readonly FakeTimeProvider _clock = new(_now);

    public RunRowWhenTests() => _clock.SetLocalTimeZone(TimeZoneInfo.Utc);

    private static DateTimeOffset At(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

    private RunRowViewModel Ended(string ended) => new(new WorkflowRunRowView("morning-check@1:1", 1, "succeeded", "schedule", At(ended).AddSeconds(-10), At(ended), Error: null), _clock);

    private RunRowViewModel Running(string started) => new(new WorkflowRunRowView("morning-check@1:1", 1, "running", "schedule", At(started), EndedAt: null, Error: null), _clock);

    [Fact]
    public void A_run_under_a_day_old_keeps_its_age() =>
        Assert.Equal("Ended 5m ago", Ended("2026-03-10T08:55:00Z").WhenText);

    [Fact]
    public void A_run_from_yesterday_past_a_day_says_yesterday_and_the_time() =>
        Assert.Equal("Ended yesterday 07:00", Ended("2026-03-09T07:00:00Z").WhenText);

    [Fact]
    public void An_older_run_this_year_says_the_day_and_the_time() =>
        Assert.Equal("Ended Mar 1 07:00", Ended("2026-03-01T07:00:00Z").WhenText);

    [Fact]
    public void A_run_from_another_year_says_the_year_too() =>
        Assert.Equal("Ended Nov 10 2025 07:00", Ended("2025-11-10T07:00:00Z").WhenText);

    [Fact]
    public void A_run_still_running_after_a_day_says_when_it_started() =>
        Assert.Equal("Started Mar 8 07:00", Running("2026-03-08T07:00:00Z").WhenText);

    [Fact]
    public void The_day_and_time_are_local()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test+2", "test+2"));

        var row = Ended("2026-03-01T07:00:00Z");

        Assert.Equal(("Ended Mar 1 09:00", "Sun 1 Mar 2026 · 09:00:00"), (row.WhenText, row.WhenTip));
    }

    [Fact]
    public void The_tip_gives_the_exact_moment_at_any_age()
    {
        Assert.Equal("Mon 9 Mar 2026 · 07:00:00", Ended("2026-03-09T07:00:00Z").WhenTip);
        Assert.Equal("Tue 10 Mar 2026 · 08:55:00", Ended("2026-03-10T08:55:00Z").WhenTip);
    }

    [Fact]
    public void The_accessible_name_says_the_same_time()
    {
        var row = Ended("2026-03-01T07:00:00Z");

        Assert.Contains("ended Mar 1 07:00", row.Name, StringComparison.OrdinalIgnoreCase);
    }
}
