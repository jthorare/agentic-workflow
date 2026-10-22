using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet's reads and words (collector spec CU-3), every value a field of a sessions-id golden.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionSheetViewModelTests
{
    private const string ClaudeRef = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string CodexRef = "codex/bbbbbbbb-0000-4000-8000-000000000002";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Value(IEnumerable<SessionFieldViewModel> fields, string label) => fields.Single(f => f.Label == label).Value;

    private async Task<SessionSheetViewModel> Opened(string reference, bool catalog = true)
    {
        var sheet = new SessionSheetViewModel(_server);
        if (catalog)
        {
            var shown = new CatalogViewModel(_clipboard, _clock);
            shown.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
            sheet.Wire(shown, _clock);
        }

        await sheet.Open(_api, reference, Token);
        return sheet;
    }

    [Fact]
    public async Task Opens_from_the_ref_and_reads_session_once()
    {
        var sheet = await Opened(ClaudeRef);

        Assert.Equal((FramePhase.Shown, ClaudeRef, ClaudeRef, 1), (sheet.Phase, sheet.Ref, _server.LastSessionRef, _server.SessionCalls));
        Assert.Equal(("Claude Code private title", "● Working"), (sheet.Heading, sheet.StateLine));
    }

    [Fact]
    public async Task Shows_cwd_socket_pane_and_clients_from_claude_tmux()
    {
        var sheet = await Opened(ClaudeRef);

        Assert.Equal("/work/api/.claude/worktrees/ideas", Value(sheet.Fields, "Working directory"));
        Assert.Equal(("tmux · Warp", "default", "main", "%3", "/tmp/tmux-1000/default", "1"), (Value(sheet.HostFields, "Host"), Value(sheet.HostFields, "Socket kind"), Value(sheet.HostFields, "Tmux session"), Value(sheet.HostFields, "Pane"), Value(sheet.HostFields, "Socket"), Value(sheet.HostFields, "Attached clients")));
        Assert.Equal(("api", ".claude/worktrees/ideas", "sindri / ideas", "111"), (Value(sheet.Fields, "Repo"), Value(sheet.Fields, "Worktree"), Value(sheet.Fields, "Lane"), Value(sheet.Fields, "Process")));
        Assert.Equal("Socket: /tmp/tmux-1000/default", sheet.HostFields.Single(f => f.Label == "Socket").Name);
    }

    [Fact]
    public async Task Codex_hook_row_has_no_tmux_details()
    {
        var sheet = await Opened(CodexRef);

        Assert.Equal(("Codex web/bbbbbbbb", "◐ Asking you · approval · Bash"), (sheet.Heading, sheet.StateLine));
        Assert.Equal(["Host", "Terminal"], sheet.HostFields.Select(f => f.Label), StringComparer.Ordinal);
        Assert.Equal(("Warp", "warp", "/work/web"), (Value(sheet.HostFields, "Host"), Value(sheet.HostFields, "Terminal"), Value(sheet.Fields, "Working directory")));
        Assert.DoesNotContain(sheet.Fields, f => new[] { "Worktree", "Lane" }.Contains(f.Label, StringComparer.Ordinal));
        Assert.False(sheet.HasHistory);
    }

    [Fact]
    public async Task State_history_is_listed_oldest_first_with_source()
    {
        var sheet = await Opened(ClaudeRef);

        Assert.Equal(["○ Idle", "◐ Asking you", "● Working"], sheet.History.Select(h => h.Chip.Text), StringComparer.Ordinal);
        Assert.Equal([false, false, true], sheet.History.Select(h => h.IsNow));
        Assert.Equal("◐ Asking you · approval · 2026-10-16 11:59:00 UTC · hook", sheet.History[1].Name);
        Assert.EndsWith("· now · record", sheet.History[2].Detail, StringComparison.Ordinal);
        Assert.True(sheet.HasHistory);
    }

    [Fact]
    public async Task Hidden_fields_null_draw_as_absent_not_empty_labels()
    {
        _server.SessionAnswer = RouteAnswer.Api("sessions-id", "session-caller-hidden-fields");
        var sheet = await Opened(ClaudeRef);

        Assert.Equal(FramePhase.Shown, sheet.Phase);
        Assert.All(sheet.Fields.Concat(sheet.HostFields), f => Assert.False(string.IsNullOrWhiteSpace(f.Value), f.Label));
        Assert.DoesNotContain(sheet.Fields, f => f.Label == "Process");
        Assert.DoesNotContain(sheet.HostFields, f => new[] { "Tmux session", "Pane", "Socket" }.Contains(f.Label, StringComparer.Ordinal));
        Assert.EndsWith("aaaaaaaa", sheet.Heading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Not_found_reads_this_session_has_ended_and_keeps_the_sheet_open()
    {
        _server.SessionAnswer = RouteAnswer.Problem("sessions-id", "not-found");
        var sheet = await Opened("codex/gone");

        Assert.Equal((FramePhase.NotFound, true, "This session has ended.", false), (sheet.Phase, sheet.IsEnded, sheet.EndedText, sheet.IsFocusEnabled));
        Assert.Equal("no live session has that ref (or it is outside this caller's repo)", sheet.EndedDetail);
        Assert.Empty(sheet.ProblemText);
    }

    [Fact]
    public async Task A_session_that_ends_while_shown_keeps_its_fields_and_says_so()
    {
        var sheet = await Opened(ClaudeRef);
        _server.SessionAnswer = RouteAnswer.Problem("sessions-id", "not-found");

        await sheet.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, true, "This session has ended."), (sheet.Phase, sheet.IsEnded, sheet.EndedText));
        Assert.Equal("/work/api/.claude/worktrees/ideas", Value(sheet.Fields, "Working directory"));
    }

    [Fact]
    public async Task Another_failure_keeps_the_last_fields_with_a_note_or_says_why_when_none()
    {
        var unread = new SessionSheetViewModel(_server);
        _server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, "{ not json");
        await unread.Open(_api, ClaudeRef, Token);
        Assert.Equal((FramePhase.Unreadable, false), (unread.Phase, unread.ProblemText.Length == 0));

        _server.SessionAnswer = null;
        var shown = await Opened(ClaudeRef);
        _server.SessionAnswer = RouteAnswer.Problem("sessions", "unsupported-platform");
        await shown.Refresh(_api, Token);
        Assert.Equal((FramePhase.Shown, false), (shown.Phase, shown.Note.Length == 0));
        Assert.False(shown.IsEnded);
    }

    [Fact]
    public async Task Re_reads_on_refresh_and_never_before_it_has_a_session()
    {
        var sheet = new SessionSheetViewModel(_server);
        await sheet.Refresh(_api, Token);
        Assert.Equal(0, _server.SessionCalls);

        await sheet.Open(_api, ClaudeRef, Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal(2, _server.SessionCalls);
    }

    [Fact]
    public async Task Preview_seam_is_null()
    {
        var sheet = await Opened(ClaudeRef);

        Assert.Null(sheet.Preview);
    }

    [Fact]
    public async Task Focus_is_the_catalogs_pending_control_with_the_ref_filled_in()
    {
        var sheet = await Opened(CodexRef);

        Assert.True(sheet.Focus?.IsPending);
        Assert.Equal("sindri agents focus " + CodexRef, sheet.FocusCli);
        Assert.Equal("Coming soon · sindri agents focus " + CodexRef, sheet.FocusLine);
        Assert.False(sheet.IsFocusEnabled);
    }

    [Fact]
    public async Task Focus_without_a_catalog_has_no_line()
    {
        var sheet = await Opened(CodexRef, catalog: false);

        Assert.Equal((null, string.Empty, false), (sheet.Focus, sheet.FocusLine, sheet.IsFocusEnabled));
    }

    [Fact]
    public async Task Copy_puts_the_filled_cli_on_the_clipboard_and_says_Copied_for_a_moment()
    {
        var sheet = await Opened(CodexRef);

        await sheet.CopyFocus(Token);

        Assert.Equal(("sindri agents focus " + CodexRef, "Copied"), (_clipboard.Text, sheet.CopyLabel));
        _clock.Advance(ControlViewModel.CopiedFor);
        for (var i = 0; i < 400 && sheet.CopyLabel != "Copy"; i++)
        {
            await Task.Delay(5, Token);   // the continuation runs on the pool
        }

        Assert.Equal("Copy", sheet.CopyLabel);
    }

    [Fact]
    public async Task A_refused_clipboard_leaves_Copy_as_it_was()
    {
        _clipboard.Refuse = true;
        var sheet = await Opened(CodexRef);

        await sheet.CopyFocus(Token);

        Assert.Equal(("Copy", null), (sheet.CopyLabel, _clipboard.Text));
    }
}
