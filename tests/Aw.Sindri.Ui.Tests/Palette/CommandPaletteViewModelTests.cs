using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>
/// The command palette (UI spec §8): Actions (every catalog row not in Sindri, grouped by status), Go to, Sindri
/// (Rebuild views, Stop Sindri, Health); fuzzy matching on labels and ids; ↑/↓, Enter, Esc; the history of entries run.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class CommandPaletteViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();
    private readonly List<PageId> _pages = [];
    private readonly List<string> _history = [];
    private readonly CommandList _catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
    private readonly CommandPaletteViewModel _palette;
    private readonly CatalogViewModel _controls;

    public CommandPaletteViewModelTests()
    {
        _controls = new CatalogViewModel(_clipboard, _clock);
        _controls.Show(_api, _server, _catalog);
        _palette = new CommandPaletteViewModel(_controls, _pages.Add, _history.Add);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PaletteEntry Entry(string id) => _palette.Entries.Single(e => e.Id == id);

    // The U1 proof: every catalog row appears once, each with its status.
    [Fact]
    public void Open_lists_every_catalog_row_once_with_its_status()
    {
        _palette.Open();

        Assert.True(_palette.IsOpen);
        Assert.Equal(["Actions", "Go to", "Sindri"], _palette.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        var actions = _palette.Entries.Where(e => e.IsAction).Select(e => e.Id).ToList();
        Assert.Equal(_catalog.Rows.Select(r => r.Action).Order(StringComparer.Ordinal), actions.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(("ready", "coming soon", "Copy: sindri doctor"), (Entry("RebuildViews").Status, Entry("ProbeLaunchTargets").Status, Entry("RunDoctor").Status));
        Assert.Equal("Check launch targets again, coming soon", Entry("ProbeLaunchTargets").Name);
    }

    // Q3: with no selected row, Actions lists the catalog grouped by status: ready, then pending, then cli-only.
    [Fact]
    public void Actions_are_grouped_by_status_and_rebuild_and_stop_sit_in_sindri()
    {
        _palette.Open();

        var actions = _palette.Sections[0].Entries;
        var kinds = actions.Select(e => e.Control!.Kind).ToList();
        Assert.Equal(kinds.Order(), kinds);
        Assert.DoesNotContain("RebuildViews", actions.Select(e => e.Id), StringComparer.Ordinal);
        Assert.DoesNotContain("StopProcess", actions.Select(e => e.Id), StringComparer.Ordinal);
        Assert.Equal(["Rebuild views", "Stop Sindri", "System"], _palette.Sections[2].Entries.Select(e => e.Label), StringComparer.Ordinal);
    }

    [Fact]
    public void Go_to_lists_the_pages_with_their_keys()
    {
        _palette.Open();

        Assert.Equal(
            ["Home G H", "Workflows G W", "Work G Q", "Workflow types G R", "Context G C", "Agents G A", "System G .", "Jobs G J", "Settings ⌘,"],
            _palette.Sections[1].Entries.Select(e => e.Label + " " + e.Key),
            StringComparer.Ordinal);
    }

    // §8: fuzzy matching on labels and ids.
    [Theory]
    [InlineData("rebuild", new[] { "RebuildViews" })]
    [InlineData("REBVIEWS", new[] { "RebuildViews" })]
    [InlineData("probelaunch", new[] { "ProbeLaunchTargets" })]
    [InlineData("stop sin", new[] { "StopProcess" })]
    public void Typing_filters_by_label_and_id(string query, string[] ids)
    {
        _palette.Open();

        _palette.Query = query;

        Assert.Equal(ids, _palette.Entries.Select(e => e.Id), StringComparer.Ordinal);
        Assert.All(_palette.Sections, s => Assert.NotEmpty(s.Entries));
    }

    [Fact]
    public void Nothing_matching_leaves_no_sections_and_no_selection()
    {
        _palette.Open();

        _palette.Query = "zzzz";

        Assert.Empty(_palette.Sections);
        Assert.Null(_palette.Selected);
    }

    // ↑/↓ move between entries that run; a pending entry is shown but never selected.
    [Fact]
    public void Arrows_move_the_selection_over_entries_that_run()
    {
        _palette.Open();
        Assert.Equal("ApplyCommand:answer-accept", _palette.Selected?.Id);   // no ready row is in Actions: the first that runs is a copy block

        _palette.Move(1);
        Assert.Equal("ApplyCommand:answer-grant", _palette.Selected?.Id);
        _palette.Move(-1);
        _palette.Move(-1);
        Assert.Equal("ApplyCommand:answer-accept", _palette.Selected?.Id);
        Assert.True(_palette.Selected!.IsSelected);
        Assert.All(_palette.Entries.Where(e => e.Control?.IsPending == true), e => Assert.False(e.IsEnabled));
    }

    // A pending row says "coming soon" in its status and its accessible name; no phase code reaches the screen.
    [Fact]
    public void Pending_entries_say_coming_soon_with_no_phase_code()
    {
        _palette.Open();
        var pending = _palette.Entries.Where(e => e.Control?.IsPending == true).ToList();

        Assert.NotEmpty(pending);
        Assert.All(pending, e =>
        {
            Assert.Equal("coming soon", e.Status);
            Assert.DoesNotContain("arrives", e.Name, StringComparison.Ordinal);
        });
    }

    // Q4: Stop Sindri goes to Health, where it lives (§4.8); it is never sent from the palette.
    [Fact]
    public async Task Stop_sindri_goes_to_health_and_closes()
    {
        _palette.Open();

        await _palette.Run(Entry("StopProcess"), Token);

        Assert.Equal([PageId.System], _pages);
        Assert.False(_palette.IsOpen);
        Assert.Equal(["StopProcess"], _history, StringComparer.Ordinal);
    }

    // F1: it opens, and every refilter shows, at the top (the first heading in view) with the first entry that runs
    // selected; only the arrows make the list follow the selection.
    [Fact]
    public void Open_and_every_refilter_start_at_the_top_and_only_the_arrows_follow_the_selection()
    {
        _palette.Open();
        Assert.Equal(("ApplyCommand:answer-accept", false), (_palette.Selected?.Id, _palette.FollowsSelection));

        _palette.Move(1);
        Assert.True(_palette.FollowsSelection);

        _palette.Query = "o";
        Assert.False(_palette.FollowsSelection);
        Assert.Same(_palette.Entries.First(e => e.IsEnabled), _palette.Selected);

        _palette.Move(1);
        _palette.Close();
        _palette.Open();
        Assert.False(_palette.FollowsSelection);
    }

    // F2 (provisional default): a ready command keeps the palette open and its entry shows §4.1's lifecycle: sending
    // (disabled, label kept), a second Enter does nothing, then done for 2 s, then ready again.
    [Fact]
    public async Task A_ready_entry_stays_open_and_shows_sending_then_done()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        var rebuild = Entry("RebuildViews");

        var run = _palette.Run(rebuild, Token);

        _clock.Advance(ControlViewModel.SendingAfter);

        Assert.Equal((true, true, false, "Rebuild views", "Sending…"), (_palette.IsOpen, rebuild.IsSending, rebuild.IsEnabled, rebuild.Label, rebuild.Status));
        await _palette.Run(rebuild, Token);
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, _server.RebuildCalls);
        Assert.Equal((true, false, true, "Rebuild views: done"), (_palette.IsOpen, rebuild.IsSending, rebuild.IsEnabled, rebuild.Status));
        Assert.Equal("Rebuild views: done", rebuild.Name);
        Assert.Equal(["RebuildViews"], _history, StringComparer.Ordinal);
        _clock.Advance(ControlViewModel.AcceptedFor);
        Assert.Equal("ready", rebuild.Status);
    }

    // G3 (a11y-0, corr-2): the selected entry that starts sending stays counted ("n of m", never "0 of"), the palette says
    // "Sending…" only after the control does (300 ms), and arrowing away from it goes to its neighbour.
    [Fact]
    public async Task A_selection_that_is_sending_keeps_its_place_and_says_sending_only_after_300_ms()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected!;
        var position = PositionOf(_palette.Announcement);
        var run = _palette.Run(rebuild, Token);

        Assert.Equal(position, PositionOf(_palette.Announcement));
        Assert.DoesNotContain("Sending", _palette.Announcement, StringComparison.Ordinal);
        Assert.Equal(string.Empty, rebuild.Status);

        _clock.Advance(ControlViewModel.SendingAfter);

        Assert.StartsWith("Rebuild views, Sending…, ", _palette.Announcement, StringComparison.Ordinal);
        Assert.Equal(position, PositionOf(_palette.Announcement));
        var next = Neighbour(rebuild, by: 1);
        _palette.Move(1);
        Assert.Same(next, _palette.Selected);
        _server.RebuildGate.SetResult();
        await run;
    }

    // H5 (test-1): arrowing up off a sending selection goes to the entry above it, not to the first one. Before G3 the
    // sending entry was not among the stops, its index was -1, and any move landed on the first entry that runs.
    [Fact]
    public async Task Arrowing_up_from_a_sending_selection_goes_to_the_previous_entry_that_runs()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected!;
        var run = _palette.Run(rebuild, Token);
        var previous = Neighbour(rebuild, by: -1);

        _palette.Move(-1);

        Assert.Same(previous, _palette.Selected);
        Assert.NotSame(_palette.Entries.First(e => e.IsEnabled), previous);
        _server.RebuildGate.SetResult();
        await run;
    }

    // H6 (a11y-1): the "n of m" counts every entry that runs, the sending one too; it is skipped only as a stop. So moving
    // off it says the next position of the same total, not a position of one fewer.
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Moving_off_a_sending_selection_keeps_the_positions_of_the_same_total(int by)
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var (position, total) = PositionOf(_palette.Announcement);
        var run = _palette.Run(_palette.Selected, Token);
        _clock.Advance(ControlViewModel.SendingAfter);

        _palette.Move(by);

        Assert.Equal((position + by, total), PositionOf(_palette.Announcement));
        _server.RebuildGate.SetResult();
        await run;
    }

    // The "n of m" at the end of an announcement.
    private static (int Position, int Total) PositionOf(string announcement)
    {
        var tail = announcement[(announcement.LastIndexOf(", ", StringComparison.Ordinal) + 2)..].Split(" of ");
        return (int.Parse(tail[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(tail[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    // H6 (a11y-2, J1): a run from the palette is announced once. The palette speaks the lifecycle of what it ran and mutes that
    // control only, so the control's own live region stays dark; once the palette is closed (Esc while sending) the control
    // speaks the outcome.
    [Fact]
    public async Task A_run_from_the_palette_is_announced_by_the_palette_and_not_by_the_control()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected!;
        var live = new List<bool>();
        rebuild.Control!.PropertyChanged += (_, e) => live.Add(e.PropertyName == nameof(ControlViewModel.IsAnnouncing) && rebuild.Control.IsAnnouncing);

        var run = _palette.Run(rebuild, Token);
        _clock.Advance(ControlViewModel.SendingAfter);
        _server.RebuildGate.SetResult();
        await run;

        Assert.DoesNotContain(true, live);
        Assert.False(rebuild.Control.IsAnnouncing);
        Assert.StartsWith("Rebuild views: done, ", _palette.Announcement, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_control_speaks_its_outcome_when_the_palette_closed_while_it_sent()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        var rebuild = Entry("RebuildViews");
        var run = _palette.Run(rebuild, Token);

        _palette.Close();
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal((true, "Rebuild views: done"), (rebuild.Control!.IsAnnouncing, rebuild.Control.Announcement));
    }

    // The nearest entry that runs above (-1) or below (+1) an entry in the list.
    private PaletteEntry Neighbour(PaletteEntry from, int by)
    {
        var at = _palette.Entries.ToList().IndexOf(from);
        return by > 0 ? _palette.Entries.Skip(at + 1).First(e => e.IsEnabled) : _palette.Entries.Take(at).Last(e => e.IsEnabled);
    }

    // H1 (corr-0): the palette built for the first time while a command is in flight must still run that entry once the
    // send is over (its Runs is the control's sendability, not CanRun at build time).
    [Fact]
    public async Task A_palette_first_opened_while_a_command_sends_runs_that_entry_afterwards()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var control = _controls.For("RebuildViews")!;
        var first = control.Press(Token);

        _palette.Open();
        var rebuild = Entry("RebuildViews");
        _server.RebuildGate.SetResult();
        await first;
        _server.RebuildGate = null;
        await _palette.Run(rebuild, Token);

        Assert.Equal((true, 2), (rebuild.IsEnabled, _server.RebuildCalls));
    }

    // H2 (a11y-0): while a command sends, the palette's live region never says "ready": not in the first 300 ms, not at
    // "Sending…", and not between "Sending…" and the outcome (the order the control clears its flags in).
    [Theory]
    [InlineData(false, "Rebuild views: done, ")]
    [InlineData(true, "Rebuild views, SND-API-005")]
    public async Task A_send_is_never_announced_as_ready_on_the_way_to_its_outcome(bool refused, string outcome)
    {
        _server.RebuildGate = new TaskCompletionSource();
        if (refused)
        {
            _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        }

        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var said = new List<string>();
        _palette.PropertyChanged += (_, e) => said.Add(e.PropertyName == nameof(CommandPaletteViewModel.Announcement) ? _palette.Announcement : string.Empty);
        var run = _palette.Run(_palette.Selected, Token);
        _clock.Advance(ControlViewModel.SendingAfter);
        _server.RebuildGate.SetResult();
        await run;

        var spoken = said.Where(t => t.Length > 0).ToList();
        Assert.DoesNotContain(spoken, t => t.Contains("ready", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spoken, t => t.StartsWith("Rebuild views, Sending…, ", StringComparison.Ordinal));
        Assert.StartsWith(outcome, spoken[^1], StringComparison.Ordinal);
    }

    // H2: nor in the first 300 ms, when the control is still silent: the status is empty then, not "ready".
    [Fact]
    public async Task A_send_in_its_first_300_ms_is_not_called_ready()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected!;
        var spoken = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement))
            {
                spoken.Add(_palette.Announcement);
            }
        };
        var run = _palette.Run(rebuild, Token);

        Assert.Empty(spoken);   // J2: nothing is said before "Sending…": no "Rebuild views, 13 of 15" again, and never "ready"
        Assert.Equal(("Rebuild views", string.Empty), (rebuild.Name, rebuild.Status));
        _server.RebuildGate.SetResult();
        await run;
    }

    // J1 (corr-0, a11y-0): the palette speaks the lifecycle of every entry IT ran, whatever the selection or the query becomes
    // while it sends, and only that entry's control is dark; a refusal is spoken once, by the palette.
    private List<string> Spoken()
    {
        var spoken = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement))
            {
                spoken.Add(_palette.Announcement);
            }
        };
        return spoken;
    }

    private async Task RunRefusedWhile(Action change, Action? afterOpen = null)
    {
        _server.RebuildGate = new TaskCompletionSource();
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        _palette.Open();
        afterOpen?.Invoke();
        var rebuild = Entry("RebuildViews");
        var spoken = Spoken();
        var control = rebuild.Control!;
        var controlSpoke = false;
        control.PropertyChanged += (_, e) => controlSpoke |= e.PropertyName == nameof(ControlViewModel.IsAnnouncing) && control.IsAnnouncing;

        var run = _palette.Run(rebuild, Token);
        change();
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, spoken.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.False(controlSpoke);
    }

    [Fact]
    public async Task A_refusal_is_spoken_once_by_the_palette_after_the_selection_moved_off_the_entry()
    {
        await RunRefusedWhile(
            () => _palette.Move(1),
            () =>
            {
                for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
                {
                    _palette.Move(1);
                }
            });
        Assert.NotEqual("RebuildViews", _palette.Selected?.Id, StringComparer.Ordinal);   // the selection really moved off the entry that was run
    }

    [Fact]
    public async Task A_refusal_is_spoken_once_by_the_palette_after_the_query_filtered_the_entry_out() =>
        await RunRefusedWhile(() => _palette.Query = "recipes");

    [Fact]
    public async Task A_run_on_a_control_the_catalog_replaced_before_the_run_is_spoken_once() =>
        await RunRefusedWhile(() => { }, () => _controls.Show(_api, _server, _catalog));

    [Fact]
    public async Task A_send_that_did_not_start_from_the_palette_is_spoken_by_its_control_and_not_by_the_palette()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        var control = _controls.For("RebuildViews")!;
        var run = control.Press(Token);
        _palette.Open();
        var spoken = Spoken();
        var said = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                said.Add(control.Announcement);
            }
        };

        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, said.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.Empty(spoken);   // the palette said nothing: the run did not start here
    }

    // J4 (corr-2): the accepted outcome settling back to idle (2 s later) is not announced: no unprompted "ready".
    [Fact]
    public async Task The_accepted_outcome_settling_to_idle_is_not_announced()
    {
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected!;
        await _palette.Run(rebuild, Token);
        var spoken = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement))
            {
                spoken.Add(_palette.Announcement);
            }
        };

        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal("ready", rebuild.Status);
        Assert.Empty(spoken);
    }

    // J3 (corr-1): a retry from Failed or from Accepted clears the old outcome only after the phase is Sending, so the entry's
    // name is never the half-cleared ", :" (or an empty status that leaves ", 3 of 14").
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_retry_never_announces_a_half_cleared_outcome(bool fromFailed)
    {
        if (fromFailed)
        {
            _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        }

        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        var rebuild = _palette.Selected;
        await _palette.Run(rebuild, Token);
        _server.Rebuild = RouteAnswer.SpecShaped("views-rebuild", "accepted");
        var spoken = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement))
            {
                spoken.Add(_palette.Announcement);
            }
        };

        await _palette.Run(rebuild, Token);

        Assert.Contains(spoken, t => t.StartsWith("Rebuild views: done", StringComparison.Ordinal));
        Assert.All(spoken, t =>
        {
            Assert.DoesNotContain(", :", t, StringComparison.Ordinal);
            Assert.False(t.StartsWith(',') || t.Length == 0);
        });
    }

    // F2: a refusal shows Sindri's code as the entry's status and its detail as served; the palette stays open.
    [Fact]
    public async Task A_refused_ready_entry_shows_sindris_code_and_detail()
    {
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005",
            "POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it");
        _palette.Open();
        var rebuild = Entry("RebuildViews");

        await _palette.Run(rebuild, Token);

        Assert.Equal((true, true, "SND-API-005"), (_palette.IsOpen, rebuild.IsFailed, rebuild.Status));
        Assert.Equal("POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it", rebuild.ErrorDetail);
        Assert.Equal("Rebuild views, SND-API-005: POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it", rebuild.Name);
        Assert.True(rebuild.IsEnabled);
    }

    // F2: Esc closes the palette at any point; the command keeps running and its outcome lands on the control.
    [Fact]
    public async Task Esc_while_sending_closes_and_the_command_keeps_running()
    {
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();
        var rebuild = Entry("RebuildViews");
        var run = _palette.Run(rebuild, Token);

        _palette.Close();
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal((false, 1, true), (_palette.IsOpen, _server.RebuildCalls, rebuild.Control!.IsAccepted));
    }

    // The policy's other side, so switching it is one line: close first, then send.
    [Fact]
    public async Task The_close_first_policy_closes_before_sending()
    {
        _palette.RunPolicy = PaletteRunPolicy.CloseFirst;
        _server.RebuildGate = new TaskCompletionSource();
        _palette.Open();

        var run = _palette.Run(Entry("RebuildViews"), Token);

        Assert.False(_palette.IsOpen);
        _server.RebuildGate.SetResult();
        await run;
        Assert.Equal(1, _server.RebuildCalls);
    }

    // A cli-only entry copies its CLI and says "Copied" for 1.5 s; the palette stays open.
    [Fact]
    public async Task A_cli_only_entry_copies_its_cli()
    {
        _palette.Open();
        _palette.Query = "approve";

        await _palette.Run(_palette.Selected, Token);

        Assert.Equal("sindri profile approve [<hash>]", _clipboard.Text);
        Assert.Equal(("Copied", true), (Entry("ApproveProfile").Status, _palette.IsOpen));
        _clock.Advance(ControlViewModel.CopiedFor);
        Assert.Equal("Copy: sindri profile approve [<hash>]", Entry("ApproveProfile").Status);
    }

    [Fact]
    public async Task A_pending_entry_does_nothing()
    {
        _palette.Open();

        await _palette.Run(Entry("CancelJob"), Token);

        Assert.Equal((true, 0, null), (_palette.IsOpen, _history.Count, _clipboard.Text));
    }

    [Fact]
    public async Task A_go_to_entry_shows_its_page()
    {
        _palette.Open();
        _palette.Query = "recipes";

        await _palette.Run(_palette.Selected, Token);

        Assert.Equal([PageId.Recipes], _pages);
        Assert.Equal(["go:Recipes"], _history, StringComparer.Ordinal);
        Assert.False(_palette.IsOpen);
    }

    [Fact]
    public void Close_clears_the_query()
    {
        _palette.Open();
        _palette.Query = "rebuild";

        _palette.Close();
        _palette.Open();

        Assert.Equal((true, string.Empty), (_palette.IsOpen, _palette.Query));
        Assert.Equal(3, _palette.Sections.Count);
    }

    // F1 (a11y-0, a11y-1): the live region is never empty (Avalonia #22312) and says the selected entry on every change.
    [Fact]
    public void The_announcement_is_never_empty_and_names_the_selected_entry()
    {
        var seen = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement))
            {
                seen.Add(_palette.Announcement);
            }
        };

        _palette.Open();
        Assert.Contains(_palette.Selected!.Name, _palette.Announcement, StringComparison.Ordinal);

        _palette.Move(1);
        Assert.Contains(_palette.Selected!.Name, _palette.Announcement, StringComparison.Ordinal);
        Assert.Contains("2 of", _palette.Announcement, StringComparison.Ordinal);

        _palette.Query = "workflow types";
        Assert.Contains("Workflow types", _palette.Announcement, StringComparison.Ordinal);

        _palette.Query = "zzzz";
        Assert.Equal("Nothing matches", _palette.Announcement);
        Assert.All(seen, Assert.NotEmpty);
        Assert.NotEmpty(_palette.Announcement);
    }

    // F1: "Copied" after a copy reaches the live region too (the selected entry's name changes under it).
    [Fact]
    public async Task A_copy_changes_the_announcement()
    {
        _palette.Open();
        _palette.Query = "approve profile";

        await _palette.Run(_palette.Selected, Token);

        Assert.Contains("Copied", _palette.Announcement, StringComparison.Ordinal);
    }

    // F1 (arch-2, corr-5): opening again must not stack subscriptions on the long-lived controls.
    [Fact]
    public void Opening_again_reuses_the_entries_while_the_catalog_is_unchanged()
    {
        _palette.Open();
        var first = _palette.Entries.ToList();
        _palette.Close();

        _palette.Open();

        Assert.Equal(first, _palette.Entries);
    }
}
