using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IBrowserSession contract: the semantics the step runner and parity rely on, which a fake kinder than Chromium
/// would hide. Each test opens a session on the contract page at path '/', which holds: a visible text 'Hospitals'; a
/// visible button data-testid 'open' (text 'Open') whose click shows the text 'Panel open' (hidden before); a visible
/// input data-testid 'search'; a hidden button data-testid 'hidden-button'; and a visible link data-testid 'next' to
/// '/next'. The session records nothing (no record directory).
/// The script-feature tests open the feature page instead, which holds: a scroll container data-testid 'strip' whose scroll shows
/// 'Strip scrolled'; a 'card' and a 'lane' whose drag-and-drop (a real press, move and release) shows 'Dropped', or 'Dropped with
/// Alt' when Alt is held at the release; a key listener that shows 'Key &lt;key&gt;' for every key pressed; an input data-testid 'note';
/// a button 'night' that sets the query parameter timeFilter=Night; a hidden element 'ghost' that the button 'show-ghost' shows;
/// three buttons data-testid 'row' (the nth shows 'Row &lt;n&gt; clicked'); two buttons named 'Dup' (the nth shows 'Dup &lt;n&gt; clicked');
/// a button named 'Plain' with the text 'Plain'; and, on load, the text 'Theme &lt;value&gt;' for the localStorage key 'theme' ('none' when unset).
/// </summary>
public abstract class BrowserSessionContract
{
    /// <summary>How long a wait that should fail is given (the adapter really waits it out).</summary>
    protected static readonly TimeSpan FailingWait = TimeSpan.FromMilliseconds(800);

    /// <summary>How long a wait that should succeed is given.</summary>
    protected static readonly TimeSpan PassingWait = TimeSpan.FromSeconds(10);

    private static readonly Locator _open = Locator.ByTestId(TestId.From("open"));
    private static readonly Locator _missing = Locator.ByTestId(TestId.From("no-such-element"));

    /// <summary>Opens a session on the contract page (above).</summary>
    /// <returns>The session; the test disposes it.</returns>
    protected abstract Task<IBrowserSession> OpenContractPageAsync();

    /// <summary>Opens a session on the feature page (above), with the given localStorage entries in place before the page loads.</summary>
    /// <param name="localStorage">The entries the session was opened with; null for none.</param>
    /// <returns>The session; the test disposes it.</returns>
    protected abstract Task<IBrowserSession> OpenFeaturePageAsync(IReadOnlyDictionary<string, string>? localStorage = null);

    /// <summary>A visible element is found; a missing one is AW-EVIDENCE-013 once the wait runs out.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_visible_element_is_found_and_a_missing_one_is_a_browser_failure()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitVisibleAsync(_open, PassingWait, CancellationToken.None)).IsSuccess, "a visible test id is found");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Hospitals"), PassingWait, CancellationToken.None)).IsSuccess, "a visible text is found");
        IsBrowserFailure(await session.WaitVisibleAsync(_missing, FailingWait, CancellationToken.None), "a missing element");
    }

    /// <summary>A hidden or absent text waits as hidden; a visible one doesn't.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_hidden_text_is_hidden_and_a_visible_one_is_not()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitHiddenAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "a hidden text is hidden");
        IsBrowserFailure(await session.WaitHiddenAsync(Locator.ByText("Hospitals"), FailingWait, CancellationToken.None), "a visible text waited on as hidden");
    }

    /// <summary>A click runs the element's effect.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_click_runs_the_elements_effect()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.ClickAsync(_open, PassingWait, CancellationToken.None)).IsSuccess, "the click succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "its effect shows");
    }

    /// <summary>A click on a missing element and a fill on a hidden one are browser failures.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Acting_on_a_missing_or_hidden_element_is_a_browser_failure()
    {
        await using var session = await OpenContractPageAsync();

        IsBrowserFailure(await session.ClickAsync(_missing, FailingWait, CancellationToken.None), "a click on a missing element");
        IsBrowserFailure(await session.FillAsync(Locator.ByTestId(TestId.From("hidden-button")), "x", FailingWait, CancellationToken.None), "a fill on a hidden element");
    }

    /// <summary>A filled input reads back its value.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_filled_input_reads_back_its_value()
    {
        await using var session = await OpenContractPageAsync();
        var search = Locator.ByTestId(TestId.From("search"));

        Expect.That((await session.FillAsync(search, "St", PassingWait, CancellationToken.None)).IsSuccess, "the fill succeeds");
        Expect.That((await session.InputValueAsync(search, PassingWait, CancellationToken.None)).Value == "St", "the value reads back");
    }

    /// <summary>The path wait holds for the current path, fails for another, and follows a navigation.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_path_wait_follows_the_page()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitForPathAsync("/", PassingWait, CancellationToken.None)).IsSuccess, "the current path holds");
        IsBrowserFailure(await session.WaitForPathAsync("/elsewhere", FailingWait, CancellationToken.None), "another path");
        Expect.That((await session.ClickAsync(Locator.ByTestId(TestId.From("next")), PassingWait, CancellationToken.None)).IsSuccess, "the link is clicked");
        Expect.That((await session.WaitForPathAsync("/next", PassingWait, CancellationToken.None)).IsSuccess, "the path follows the navigation");
    }

    /// <summary>A goto target that could leave the host is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_goto_target_that_could_leave_the_host_is_refused()
    {
        await using var session = await OpenContractPageAsync();

        IsBrowserFailure(await session.GotoAsync("//evil.test/", PassingWait, CancellationToken.None), "a scheme-relative goto");
        Expect.That((await session.WaitForPathAsync("/", PassingWait, CancellationToken.None)).IsSuccess, "the page stays where it was");
    }

    /// <summary>A visible element has a box; a missing one is a browser failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_visible_element_has_a_bounding_box_and_a_missing_one_has_none()
    {
        await using var session = await OpenContractPageAsync();

        var box = await session.BoundingBoxAsync(_open, PassingWait, CancellationToken.None);

        Expect.That(box.IsSuccess && box.Value.W > 0 && box.Value.H > 0, "a visible element has a box with a size");
        IsBrowserFailure(await session.BoundingBoxAsync(_missing, FailingWait, CancellationToken.None), "a missing element's box");
    }

    /// <summary>Visible candidates list the visible controls with their role, never a hidden one, and a candidate can be clicked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Visible_candidates_list_visible_controls_and_can_be_clicked()
    {
        await using var session = await OpenContractPageAsync();

        var candidates = (await session.VisibleCandidatesAsync(CancellationToken.None)).Value;
        var open = candidates.Single(static c => c.TestId?.Value == "open");

        Expect.That(open.Role == "button", "a button's role is 'button'");
        Expect.That(candidates.All(static c => c.TestId?.Value != "hidden-button"), "a hidden control is never offered");
        Expect.That((await session.ClickAsync(new CandidateLocator(open.Index, open.TestId), PassingWait, CancellationToken.None)).IsSuccess, "a candidate is clicked by index");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "its effect shows");
    }

    /// <summary>A scroll moves a container (and lets layout settle); a missing container is a browser failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_scroll_moves_the_container_and_a_missing_one_is_a_browser_failure()
    {
        await using var session = await OpenFeaturePageAsync();

        Expect.That((await session.ScrollAsync(Locator.ByTestId(TestId.From("strip")), "end", PassingWait, CancellationToken.None)).IsSuccess, "the scroll succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Strip scrolled"), PassingWait, CancellationToken.None)).IsSuccess, "the container scrolled");
        IsBrowserFailure(await session.ScrollAsync(_missing, "end", FailingWait, CancellationToken.None), "a scroll of a missing container");
    }

    /// <summary>A drag presses on the source, moves and releases on the destination; Alt is held through the release when asked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_drag_drops_on_the_destination_and_holds_alt_when_asked()
    {
        await using var session = await OpenFeaturePageAsync();
        var (card, lane) = (Locator.ByTestId(TestId.From("card")), Locator.ByTestId(TestId.From("lane")));

        Expect.That((await session.DragAsync(card, lane, new DragGesture(Grab: null, Drop: null, HoldAlt: false), PassingWait, CancellationToken.None)).IsSuccess, "the drag succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Dropped"), PassingWait, CancellationToken.None)).IsSuccess, "the drop shows");
        Expect.That((await session.WaitHiddenAsync(Locator.ByText("Dropped with Alt"), PassingWait, CancellationToken.None)).IsSuccess, "no Alt was held");
        Expect.That(
            (await session.DragAsync(card, lane, new DragGesture(new Offset(4, 4), new Offset(8, 8), HoldAlt: true), PassingWait, CancellationToken.None)).IsSuccess,
            "the drag with offsets and Alt succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Dropped with Alt"), PassingWait, CancellationToken.None)).IsSuccess, "Alt was held at the release");
    }

    /// <summary>A drag whose source or destination is missing fails before the press.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_drag_with_a_missing_end_is_a_browser_failure_and_drops_nothing()
    {
        await using var session = await OpenFeaturePageAsync();
        var card = Locator.ByTestId(TestId.From("card"));
        var gesture = new DragGesture(Grab: null, Drop: null, HoldAlt: false);

        IsBrowserFailure(await session.DragAsync(card, _missing, gesture, FailingWait, CancellationToken.None), "a drag to a missing destination");
        IsBrowserFailure(await session.DragAsync(_missing, card, gesture, FailingWait, CancellationToken.None), "a drag from a missing source");
        Expect.That((await session.WaitHiddenAsync(Locator.ByText("Dropped"), PassingWait, CancellationToken.None)).IsSuccess, "nothing was dropped");
    }

    /// <summary>A press sends the key to the target, or to whatever has focus; a missing target sends no key.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_press_sends_the_key_and_a_missing_target_sends_none()
    {
        await using var session = await OpenFeaturePageAsync();

        Expect.That((await session.PressAsync("ArrowRight", target: null, PassingWait, CancellationToken.None)).IsSuccess, "a press with no target succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Key ArrowRight"), PassingWait, CancellationToken.None)).IsSuccess, "the key arrived");
        Expect.That((await session.PressAsync("Enter", Locator.ByTestId(TestId.From("note")), PassingWait, CancellationToken.None)).IsSuccess, "a press on a target succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Key Enter"), PassingWait, CancellationToken.None)).IsSuccess, "the key arrived on the target");
        IsBrowserFailure(await session.PressAsync("End", _missing, FailingWait, CancellationToken.None), "a press on a missing target");
        Expect.That((await session.WaitHiddenAsync(Locator.ByText("Key End"), PassingWait, CancellationToken.None)).IsSuccess, "no key was sent");
    }

    /// <summary>The URL-search wait holds once the parameter has the value, and fails for another.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_url_search_wait_follows_one_query_parameter()
    {
        await using var session = await OpenFeaturePageAsync();

        IsBrowserFailure(await session.WaitForUrlSearchAsync("timeFilter", "Night", FailingWait, CancellationToken.None), "a parameter not set yet");
        Expect.That((await session.ClickAsync(Locator.ByTestId(TestId.From("night")), PassingWait, CancellationToken.None)).IsSuccess, "the filter is clicked");
        Expect.That((await session.WaitForUrlSearchAsync("timeFilter", "Night", PassingWait, CancellationToken.None)).IsSuccess, "the parameter holds");
        IsBrowserFailure(await session.WaitForUrlSearchAsync("timeFilter", "Day", FailingWait, CancellationToken.None), "another value");
    }

    /// <summary>An absence check looks once: it holds while no visible element has the test id and fails the moment one does.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_absence_check_fails_as_soon_as_a_visible_element_matches()
    {
        await using var session = await OpenFeaturePageAsync();
        var ghost = Locator.ByTestId(TestId.From("ghost"));

        Expect.That((await session.ExpectAbsentNowAsync(ghost, CancellationToken.None)).IsSuccess, "a hidden element is absent");
        Expect.That((await session.ClickAsync(Locator.ByTestId(TestId.From("show-ghost")), PassingWait, CancellationToken.None)).IsSuccess, "the ghost is shown");
        IsBrowserFailure(await session.ExpectAbsentNowAsync(ghost, CancellationToken.None), "a visible element");
    }

    /// <summary>A test id that repeats is ambiguous until an index picks one; role and text targets are exact and need a '#n' when they repeat.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repeated_target_needs_an_index_and_role_and_text_targets_are_exact()
    {
        await using var session = await OpenFeaturePageAsync();
        var row = TestId.From("row");

        IsBrowserFailure(await session.ClickAsync(Locator.ByTestId(row), FailingWait, CancellationToken.None), "a repeated test id without an index");
        Expect.That((await session.ClickAsync(new NthTestIdLocator(row, 1), PassingWait, CancellationToken.None)).IsSuccess, "the second row is clicked");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Row 2 clicked"), PassingWait, CancellationToken.None)).IsSuccess, "the second row answered");
        IsBrowserFailure(await session.ClickAsync(new RoleLocator("button", "Dup", Nth: null), FailingWait, CancellationToken.None), "a repeated role target without #n");
        IsBrowserFailure(await session.ClickAsync(new RoleLocator("button", "Dup", Nth: 3), FailingWait, CancellationToken.None), "a role target past the last match");
        Expect.That((await session.ClickAsync(new RoleLocator("button", "Dup", Nth: 2), PassingWait, CancellationToken.None)).IsSuccess, "the second Dup is clicked");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Dup 2 clicked"), PassingWait, CancellationToken.None)).IsSuccess, "the second Dup answered");
        Expect.That((await session.ClickAsync(new RoleLocator("button", "Plain", Nth: null), PassingWait, CancellationToken.None)).IsSuccess, "a unique role target is clicked");
        Expect.That((await session.WaitVisibleAsync(new ExactTextLocator("Plain", Nth: null), PassingWait, CancellationToken.None)).IsSuccess, "a unique exact text is found");
        IsBrowserFailure(await session.WaitVisibleAsync(new ExactTextLocator("Pla", Nth: null), FailingWait, CancellationToken.None), "a text that is only part of an element's");
    }

    /// <summary>localStorage entries are in place before the page loads.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Local_storage_entries_are_in_place_before_the_page_loads()
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal) { ["theme"] = "dark" };
        await using var withEntries = await OpenFeaturePageAsync(entries);
        await using var without = await OpenFeaturePageAsync();

        Expect.That((await withEntries.WaitVisibleAsync(Locator.ByText("Theme dark"), PassingWait, CancellationToken.None)).IsSuccess, "the page read the entry");
        Expect.That((await without.WaitVisibleAsync(Locator.ByText("Theme none"), PassingWait, CancellationToken.None)).IsSuccess, "a session without entries sees none");
    }

    /// <summary>A session without a record directory stops with no trace and no video.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_that_records_nothing_stops_with_no_trace_or_video()
    {
        await using var session = await OpenContractPageAsync();

        var recording = await session.StopRecordingAsync("/nowhere/trace.zip", CancellationToken.None);

        Expect.That(recording.IsSuccess && recording.Value.Trace is null && recording.Value.Videos.Count == 0, "nothing was recorded");
    }

    /// <summary>A cancelled token throws instead of acting.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_cancelled_call_throws()
    {
        await using var session = await OpenContractPageAsync();
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var threw = false;
        try
        {
            _ = await session.WaitVisibleAsync(_open, PassingWait, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Expect.That(threw, "a cancelled call throws OperationCanceledException");
    }

    private static void IsBrowserFailure<T>(Result<T> result, string what) =>
        Expect.That(result.IsError && result.Error.Code == EvidenceErrors.BrowserFailed.Code, $"{what} is AW-EVIDENCE-013");
}
