using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="ITray"/> passes: the fake, and the head's adapter (on the UI thread of a headless app).</summary>
public abstract class TrayContract
{
    private static readonly TrayView _neutral = new(TrayState.Neutral, "Sindri · running", "Open Sindri", "Quit");

    /// <summary>A fresh tray.</summary>
    /// <returns>The tray.</returns>
    protected abstract ITray Create();

    /// <summary>The labels the menu lists now, in order, separators left out, as the platform renders them (a '_' doubled).</summary>
    /// <param name="tray">A tray from <see cref="Create"/>.</param>
    /// <returns>The labels.</returns>
    protected abstract IReadOnlyList<string> MenuLabels(ITray tray);

    /// <summary>Picks the menu item at <paramref name="index"/> (counting separators out), as the user does.</summary>
    /// <param name="tray">A tray from <see cref="Create"/>.</param>
    /// <param name="index">The item's place in <see cref="MenuLabels"/>.</param>
    protected abstract void Pick(ITray tray, int index);

    private static TrayView WithRows(int rows, TrayState state = TrayState.Attention) => _neutral with
    {
        State = state,
        Count = rows,
        Rows = [.. Enumerable.Range(1, rows).Select(i => new TrayRow(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ITM-0{i}"), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"decision ITM-0{i} Title {i} · {i}m")))],
        RunningLabel = "Running: 3",
    };

    [Fact]
    public void It_shows_nothing_before_the_first_view() => Expect.True(Create().Shown is null);

    [Fact]
    public void It_shows_the_last_view_given()
    {
        var tray = Create();
        var notRunning = new TrayView(TrayState.NotRunning, "Sindri isn't running · run sindri", "Open Sindri", "Quit");

        tray.Show(_neutral);
        tray.Show(notRunning);

        Expect.Equal(notRunning, tray.Shown);
    }

    /// <summary>Every tray state, one theory case each: the data is the enum itself, so a new state is covered without an edit.</summary>
    public static TheoryData<TrayState> States => [.. Enum.GetValues<TrayState>()];

    // One case per state: through the Avalonia adapter each Show and Shown is dispatched to the assembly's one headless UI thread, so a single test looping every state
    // queued behind the other tests' dispatches for over a minute in the parallel integration lane. The data is discovered row by row
    // (an enum serializes), so each state is its own test case and the per-test cap times each one.
    [Theory]
    [MemberData(nameof(States))]
    public void Each_state_shows(TrayState state)
    {
        var tray = Create();
        var view = _neutral with { State = state };

        tray.Show(view);

        Expect.Equal(view, tray.Shown);
    }

    // Menu labels are the app's words; one with an underscore (an access-key marker on some platforms) shows as written.
    [Fact]
    public void Labels_with_underscores_show_as_given()
    {
        var tray = Create();
        var view = _neutral with { Tooltip = "Sindri · repo_name", OpenLabel = "Open_Sindri" };

        tray.Show(view);

        Expect.Equal(view, tray.Shown);
    }

    [Fact]
    public void Attention_shows_the_count()
    {
        var tray = Create();
        var view = WithRows(2);

        tray.Show(view);

        Expect.Equal(TrayState.Attention, tray.Shown!.State);
        Expect.Equal(2, tray.Shown.Count);
    }

    [Fact]
    public void Calm_only_shows_a_small_count_and_stays_monochrome()
    {
        var tray = Create();
        var view = WithRows(1, TrayState.Calm);

        tray.Show(view);

        // Monochrome is the head's drawing (the Avalonia tests check its pixels); the port only carries the state and count.
        Expect.Equal(TrayState.Calm, tray.Shown!.State);
        Expect.Equal(1, tray.Shown.Count);
    }

    [Fact]
    public void Menu_lists_at_most_five_rows_then_open_running_quit()
    {
        var tray = Create();

        tray.Show(WithRows(7));

        Expect.Sequence(
            [
                "decision ITM-01 Title 1 · 1m", "decision ITM-02 Title 2 · 2m", "decision ITM-03 Title 3 · 3m",
                "decision ITM-04 Title 4 · 4m", "decision ITM-05 Title 5 · 5m", "Open Sindri", "Running: 3", "Quit",
            ],
            MenuLabels(tray));
    }

    [Fact]
    public void Menu_without_rows_is_open_then_quit()
    {
        var tray = Create();

        tray.Show(_neutral);

        Expect.Sequence(["Open Sindri", "Quit"], MenuLabels(tray));
    }

    [Fact]
    public void Picking_a_row_raises_RowRequested_with_its_id_and_runs_nothing()
    {
        var tray = Create();
        var (rows, opened, quit) = (new List<string>(), 0, 0);
        tray.RowRequested += (_, e) => rows.Add(e.RowId);
        tray.OpenRequested += (_, _) => opened++;
        tray.QuitRequested += (_, _) => quit++;
        tray.Show(WithRows(3));

        Pick(tray, 1);

        Expect.Sequence(["ITM-02"], rows);
        Expect.Equal((0, 0), (opened, quit));
    }

    [Fact]
    public void Row_labels_escape_underscores()
    {
        var tray = Create();

        tray.Show(_neutral with { State = TrayState.Attention, Count = 1, Rows = [new TrayRow("ITM_01", "decision ITM_01 a_b")] });

        Expect.Equal("decision ITM__01 a__b", MenuLabels(tray)[0]);
    }
}
