using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>How a needs-you row looks by urgency, seen state and motion (UI spec §5; screens spec R8-13: tint, no side stripes).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowViewModelTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NeedsYouSeenViewModel> Panel()
    {
        var panel = new NeedsYouSeenViewModel(new Preferences(_store), _os, _clock);
        await panel.Restore(Token);
        return panel;
    }

    private static NeedsYouEntry Pulse(string id = "p") => new(id, NeedsYouUrgency.Pulse);

    [Fact]
    public async Task A_decision_row_leads_with_the_question_and_keeps_the_id_for_the_tooltip()
    {
        var panel = await Panel();
        panel.Update([new NeedsYouEntry("DEC-01a1c4", NeedsYouUrgency.Pulse) { Kind = "decision", ShownId = "DEC-01a1c4", Title = "Which retry budget should the sync worker use?" }]);

        var row = panel.Rows[0];

        Assert.Equal("Which retry budget should the sync worker use?", row.Title);
        Assert.Contains("DEC-01a1c4", row.IdTip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pulse_row_pulses_until_seen_then_the_tint_clears()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);
        var row = panel.Rows[0];
        Assert.Equal((RowTint.Pulsing, true, false, "new"), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));

        panel.Acted("p");

        Assert.Equal((RowTint.None, false, false, ""), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));
    }

    [Fact]
    public async Task Colour_row_has_a_steady_tint_and_no_motion()
    {
        var panel = await Panel();
        panel.Update([new NeedsYouEntry("c", NeedsYouUrgency.Colour)]);

        var row = panel.Rows[0];
        Assert.Equal((RowTint.Steady, false, false), (row.Tint, row.IsPulsing, row.HasOutline));

        panel.MarkAllSeen();
        Assert.Equal(RowTint.Steady, row.Tint);
    }

    [Fact]
    public async Task Calm_row_has_a_subtle_tint()
    {
        var panel = await Panel();
        panel.Update([new NeedsYouEntry("k", NeedsYouUrgency.Calm)]);

        Assert.Equal((RowTint.Subtle, false, false), (panel.Rows[0].Tint, panel.Rows[0].IsPulsing, panel.Rows[0].HasOutline));
    }

    [Fact]
    public async Task Reduced_motion_replaces_the_pulse_with_an_outline_and_the_word_new()
    {
        _os.Set(reduce: true);
        var panel = await Panel();
        panel.Update([Pulse()]);
        var row = panel.Rows[0];

        Assert.Equal((RowTint.Steady, false, true, "new"), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));

        panel.Acted("p");
        Assert.Equal((false, ""), (row.HasOutline, row.NewWord));
    }

    [Fact]
    public async Task The_in_app_switch_forces_reduced_motion_whatever_the_OS_says()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);
        Assert.False(panel.ReducedMotion);

        panel.SetInAppReduceMotion(reduce: true);

        Assert.True(panel.ReducedMotion);
        Assert.Equal((false, true, "new"), (panel.Rows[0].IsPulsing, panel.Rows[0].HasOutline, panel.Rows[0].NewWord));
    }

    [Fact]
    public async Task The_OS_setting_changing_updates_the_rows()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);

        _os.Set(reduce: true);

        Assert.True(panel.ReducedMotion);
        Assert.True(panel.Rows[0].HasOutline);
    }

    [Fact]
    public async Task New_rows_slide_in_unless_reduced()
    {
        var panel = await Panel();
        panel.Update([Pulse("a")]);
        Assert.False(panel.Rows[0].SlidesIn); // the first list is not an arrival

        panel.Update([Pulse("a"), Pulse("b")]);
        Assert.Equal((false, true), (panel.Rows[0].SlidesIn, panel.Rows[1].SlidesIn));

        _os.Set(reduce: true);
        panel.Update([Pulse("a"), Pulse("b"), Pulse("c")]);
        Assert.False(panel.Rows[2].SlidesIn);
    }

    // review run 1, corr-11: a re-read serves new, equal records; Replace reports what really changed, and only that.
    private static List<string?> Changes(NeedsYouRowViewModel row, Action change)
    {
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        change();
        return raised;
    }

    [Fact]
    public void Replace_with_an_equal_entry_reports_only_the_age()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var row = new NeedsYouRowViewModel(NeedsYouRows.Failure(), seen: true, reducedMotion: false, arrived: false, clock);

        var raised = Changes(row, () => row.Replace(NeedsYouRows.Failure()));

        Assert.Equal([nameof(NeedsYouRowViewModel.Age)], raised, StringComparer.Ordinal);
    }

    [Fact]
    public void Replace_reports_the_chip_the_details_and_the_tint_when_they_change()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var row = new NeedsYouRowViewModel(NeedsYouRows.Park(), seen: true, reducedMotion: false, arrived: false, clock);
        var louder = NeedsYouRows.Failure() with { Id = row.Id, Subject = new NeedsYouSubject("service", "index") };

        var raised = Changes(row, () => row.Replace(louder));

        Assert.Contains(nameof(NeedsYouRowViewModel.Chip), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(NeedsYouRowViewModel.Details), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(NeedsYouRowViewModel.IsSteady), raised, StringComparer.Ordinal);   // pulse (seen: none) became colour: a steady tint
        Assert.Equal(RowTint.Steady, row.Tint);
    }

    [Fact]
    public void A_new_reason_reaches_an_open_failure_sheet()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var row = new NeedsYouRowViewModel(NeedsYouRows.Failure(), seen: true, reducedMotion: false, arrived: false, clock);
        var before = row.Details.First(d => d.Label == "Why").Value;

        row.Replace(NeedsYouRows.Failure() with { Reason = "SND-SCHED-002: it failed differently" });

        Assert.NotEqual(before, row.Details.First(d => d.Label == "Why").Value, StringComparer.Ordinal);
    }
}
