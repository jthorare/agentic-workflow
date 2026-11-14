using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.NewAgent;

/// <summary>The New agent form (agents spec §15.4, §15.6, U8): focus order, Esc keeps the text, the Start button's rules, each refusal in words inside the form, and the background start with its foot line.</summary>
[Lane(TestLane.Unit)]
public sealed class NewAgentFormTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7480");
    private readonly FakeNewAgentRoute _route = new();
    private readonly FakeClipboard _clipboard = new();
    private int _ids;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NewAgentFormViewModel Form(NewAgentDefaults? defaults = null, RepoChoice[]? repos = null)
    {
        var form = new NewAgentFormViewModel(
            () => _api,
            _route,
            _clipboard,
            repos ?? [new RepoChoice("sindri", Onboarded: true), new RepoChoice("ledgerly", Onboarded: false)],
            defaults ?? new NewAgentDefaults(Repo: null, Agent: null),
            () => "act" + (++_ids).ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
        form.Open();
        return form;
    }

    [Fact]
    public void Open_puts_focus_on_Prompt_and_Tab_walks_Prompt_Repo_Agent_Worktree_Start_and_wraps()
    {
        var form = Form();
        var seen = new List<NewAgentField> { form.Focused };

        for (var i = 0; i < 5; i++)
        {
            form.MoveFocus(back: false);
            seen.Add(form.Focused);
        }

        form.MoveFocus(back: true);

        Assert.Equal(
            [NewAgentField.Prompt, NewAgentField.Repo, NewAgentField.Agent, NewAgentField.Worktree, NewAgentField.Start, NewAgentField.Prompt],
            seen);
        Assert.Equal(NewAgentField.Start, form.Focused);
    }

    [Fact]
    public void Every_field_has_a_default_so_n_then_Ctrl_Enter_is_the_whole_common_case()
    {
        var form = Form();

        Assert.Equal(("sindri", "claude-code", string.Empty, false, true), (form.Repo?.Name, form.Agent.Id, form.Prompt, form.NewWorktree, form.CanStart));
    }

    [Fact]
    public void The_last_repo_and_agent_are_the_defaults_and_an_unknown_last_repo_falls_back_to_the_first()
    {
        var kept = Form(new NewAgentDefaults("ledgerly", "codex"));
        var gone = Form(new NewAgentDefaults("deleted-repo", "nonsense"));

        Assert.Equal(("ledgerly", "codex", "sindri", "claude-code"), (kept.Repo?.Name, kept.Agent.Id, gone.Repo?.Name, gone.Agent.Id));
    }

    [Fact]
    public async Task Esc_closes_the_form_and_n_brings_back_the_same_prompt_repo_agent_and_switch()
    {
        var form = Form();
        form.Prompt = "fix the flaky test";
        form.Repo = form.Repos[1];
        form.Agent = AgentChoice.Codex;
        form.NewWorktree = true;

        var used = await form.Press("Escape", ctrl: false, shift: false, Token);
        var closed = form.IsOpen;
        form.Open();

        Assert.Equal((true, false, true, "fix the flaky test", "ledgerly", "codex", true, NewAgentField.Prompt),
            (used, closed, form.IsOpen, form.Prompt, form.Repo?.Name, form.Agent.Id, form.NewWorktree, form.Focused));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(8000, true)]
    [InlineData(8001, false)]
    public void Start_is_enabled_up_to_8000_characters_and_an_empty_prompt_is_allowed(int length, bool enabled)
    {
        var form = Form();
        form.Prompt = new string('x', length);

        Assert.Equal(enabled, form.CanStart);
    }

    [Fact]
    public void A_prompt_over_the_limit_says_how_long_it_is_under_Prompt_before_anything_is_sent()
    {
        var form = Form();
        form.Prompt = new string('x', 8412);

        Assert.Equal(("The prompt is 8,412 characters; the limit is 8,000.", 0), (form.PromptError, _route.Sent.Count));
    }

    [Fact]
    public void Start_is_off_when_the_profile_has_no_repo()
    {
        var form = Form(repos: []);

        Assert.False(form.CanStart);
    }

    [Fact]
    public async Task Start_sends_repo_agent_prompt_switch_and_the_actId_with_an_empty_or_blank_prompt_as_none()
    {
        var form = Form();
        form.Agent = AgentChoice.Codex;
        form.NewWorktree = true;
        form.Prompt = "  \n ";

        _ = await form.Press("Enter", ctrl: true, shift: false, Token);
        form.Open();
        form.Prompt = "-fix this\n\"quoted\"";
        _ = await form.Press("Enter", ctrl: true, shift: false, Token);

        Assert.Equal(
            [new("sindri", "codex", Prompt: null, Worktree: true, "act000001"), new("sindri", "codex", "-fix this\n\"quoted\"", Worktree: true, "act000002")],
            _route.Sent);
    }

    [Fact]
    public async Task While_a_start_runs_the_button_reads_Starting_the_fields_lock_and_a_second_Ctrl_Enter_sends_nothing()
    {
        var form = Form();
        form.NewWorktree = true;
        _route.Hold();

        var first = form.Press("Enter", ctrl: true, shift: false, Token);
        _ = await form.Press("Enter", ctrl: true, shift: false, Token);
        var during = (form.IsStarting, form.StartText, form.StatusText, form.CanStart, _route.Sent.Count);
        _route.Release();
        _ = await first;

        Assert.Equal((true, "Starting...", "Making the worktree", false, 1), during);
        Assert.Equal(("Start (Ctrl+Enter)", string.Empty, false), (form.StartText, form.StatusText, form.IsStarting));
    }

    [Fact]
    public async Task The_worktree_line_shows_only_when_the_switch_is_on()
    {
        var form = Form();
        _route.Hold();

        var run = form.Start(background: false, Token);
        var status = form.StatusText;
        _route.Release();
        await run;

        Assert.Equal(string.Empty, status);
    }

    [Fact]
    public async Task A_2xx_closes_the_form_opens_the_new_session_clears_the_prompt_and_the_next_start_gets_a_new_actId()
    {
        var form = Form();
        form.Prompt = "hello";
        AgentStartedViewRef? opened = null;
        form.OpenRequested += (_, e) => opened = e.Started;

        await form.Start(background: false, Token);
        form.Open();
        await form.Start(background: false, Token);

        Assert.Equal((new AgentStartedViewRef("sindri-act000002", "act000002"), string.Empty, false), (opened, form.Prompt, form.IsOpen));
        Assert.Equal(["act000001", "act000002"], _route.Sent.Select(r => r.ActId), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Error_012_shows_the_servers_words_under_Repo_and_the_form_stays_open_with_the_prompt()
    {
        var form = Form();
        form.Prompt = "keep me";
        _route.Refuse("SND-AGENT-012", "sindri: the folder ~/work/sindri is gone");

        await form.Start(background: false, Token);

        Assert.Equal(("sindri: the folder ~/work/sindri is gone", true, "keep me", string.Empty, string.Empty), (form.RepoError, form.IsOpen, form.Prompt, form.AgentError, form.FootError));
    }

    [Fact]
    public async Task Error_013_shows_under_Agent_with_the_sign_in_command_and_Copy_command_puts_it_on_the_clipboard()
    {
        var form = Form();
        _route.Refuse("SND-AGENT-013", "Claude Code is not signed in", command: "claude /login");

        await form.Start(background: false, Token);
        var copied = await form.CopySignInCommand(Token);

        Assert.Equal(("Claude Code is not signed in", "claude /login", true, "claude /login"), (form.AgentError, form.SignInCommand, copied, _clipboard.Text));
    }

    [Fact]
    public async Task Error_013_without_a_command_shows_the_plain_line_and_no_command_row()
    {
        var form = Form();
        _route.Refuse("SND-AGENT-013", "Codex is not installed");

        await form.Start(background: false, Token);

        Assert.Equal(("Codex is not installed", false, false, string.Empty), (form.AgentError, form.HasSignInCommand, await form.CopySignInCommand(Token), _clipboard.Text ?? string.Empty));
    }

    [Fact]
    public async Task Error_017_shows_the_count_under_Prompt_in_words_when_the_server_refuses()
    {
        var form = Form();
        form.Prompt = new string('x', 100);
        _route.Refuse("SND-AGENT-017", "too long");

        await form.Start(background: false, Token);

        Assert.Equal(("The prompt is 100 characters; the limit is 8,000.", 100), (form.PromptError, form.Prompt.Length));
    }

    [Fact]
    public async Task Error_014_shows_at_the_foot_with_Retry_and_Retry_sends_the_same_actId_so_it_starts_once()
    {
        var form = Form();
        form.Prompt = "go";
        _route.Refuse("SND-AGENT-014", "tmux is not installed. Run: sudo apt install tmux");

        await form.Start(background: false, Token);
        var shown = (form.FootError, form.CanRetry, form.IsOpen);
        await form.Retry(Token);

        Assert.Equal(("tmux is not installed. Run: sudo apt install tmux", true, true), shown);
        Assert.Equal(["act000001", "act000001"], _route.Sent.Select(r => r.ActId), StringComparer.Ordinal);
        Assert.Equal((string.Empty, false, false), (form.FootError, form.CanRetry, form.IsOpen));
    }

    [Fact]
    public async Task A_refusal_without_words_never_shows_the_code_alone()
    {
        var form = Form();
        _route.Refuse("SND-AGENT-014", string.Empty);
        _route.Refuse("SND-AGENT-012", "  ");

        await form.Start(background: false, Token);
        var foot = form.FootError;
        await form.Retry(Token);

        Assert.Equal((Labels.NewAgentFootFallback, Labels.NewAgentRepoFallback), (foot, form.RepoError));
        Assert.DoesNotContain("SND-AGENT", foot + form.RepoError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_a_field_clears_the_error_under_it_and_a_new_start_clears_the_foot_error()
    {
        var form = Form();
        _route.Refuse("SND-AGENT-012", "gone");
        await form.Start(background: false, Token);
        form.Repo = form.Repos[1];
        var repo = form.RepoError;
        _route.Refuse("SND-AGENT-013", "no sign-in", command: "codex login");
        await form.Start(background: false, Token);
        form.Agent = AgentChoice.Codex;

        Assert.Equal((string.Empty, string.Empty, string.Empty), (repo, form.AgentError, form.SignInCommand));
    }

    [Fact]
    public async Task Ctrl_Shift_Enter_starts_in_the_background_keeps_the_form_open_clears_the_prompt_and_shows_the_foot_line()
    {
        var form = Form();
        form.Prompt = "first";
        var opened = 0;
        form.OpenRequested += (_, _) => opened++;

        var used = await form.Press("Enter", ctrl: true, shift: true, Token);

        Assert.Equal((true, true, string.Empty, "Started claude in sindri.", 0, 1), (used, form.IsOpen, form.Prompt, form.FootLine, opened, _route.Sent.Count));
    }

    [Fact]
    public async Task Show_it_opens_the_session_the_foot_line_names()
    {
        var form = Form();
        AgentStartedViewRef? opened = null;
        form.OpenRequested += (_, e) => opened = e.Started;
        await form.Start(background: true, Token);

        form.ShowIt();

        Assert.Equal(new AgentStartedViewRef("sindri-act000001", "act000001"), opened);
    }

    [Fact]
    public async Task The_next_start_replaces_the_foot_line_and_the_second_background_start_has_its_own_actId()
    {
        var form = Form(new NewAgentDefaults("ledgerly", "codex"));

        await form.Start(background: true, Token);
        var first = form.FootLine;
        _route.Refuse("SND-AGENT-014", "tmux could not start");
        await form.Start(background: true, Token);

        Assert.Equal(("Started codex in ledgerly.", string.Empty, "tmux could not start"), (first, form.FootLine, form.FootError));
        Assert.Equal(["act000001", "act000002"], _route.Sent.Select(r => r.ActId), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_failed_background_start_keeps_the_prompt_and_shows_the_error_with_no_foot_line()
    {
        var form = Form();
        form.Prompt = "keep";
        _route.Refuse("SND-AGENT-014", "git could not make the worktree");

        await form.Start(background: true, Token);

        Assert.Equal(("keep", string.Empty, "git could not make the worktree"), (form.Prompt, form.FootLine, form.FootError));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Ctrl_Enter_and_Ctrl_Shift_Enter_start_from_any_field(int index)
    {
        var field = NewAgentFormViewModel.FocusOrder[index];
        var foreground = Form();
        while (foreground.Focused != field)
        {
            foreground.MoveFocus(back: false);
        }

        var background = Form();
        while (background.Focused != field)
        {
            background.MoveFocus(back: false);
        }

        _ = await foreground.Press("Enter", ctrl: true, shift: false, Token);
        _ = await background.Press("Enter", ctrl: true, shift: true, Token);

        Assert.Equal((false, true), (foreground.IsOpen, background.IsOpen));
        Assert.Equal(2, _route.Sent.Count);
    }

    [Fact]
    public async Task Plain_Enter_does_not_start_and_keys_on_a_closed_form_do_nothing()
    {
        var form = Form();

        var plain = await form.Press("Enter", ctrl: false, shift: false, Token);
        form.Close();
        var closed = await form.Press("Enter", ctrl: true, shift: false, Token);

        Assert.Equal((false, false, 0), (plain, closed, _route.Sent.Count));
    }

    [Fact]
    public async Task A_repo_that_is_not_onboarded_says_so_and_still_starts()
    {
        var form = Form();
        form.Repo = form.Repos[1];

        var note = form.RepoNote;
        await form.Start(background: false, Token);

        Assert.Equal(("not onboarded", "ledgerly"), (note, _route.Sent[0].Repo));
    }

    [Fact]
    public void An_onboarded_repo_has_no_note() =>
        Assert.Equal(string.Empty, Form().RepoNote);

    [Fact]
    public async Task Esc_during_a_start_is_ignored_and_the_refusal_that_follows_shows_in_the_open_form()
    {
        var form = Form();
        form.NewWorktree = true;
        _route.Hold();
        _route.Refuse("SND-AGENT-014", "git could not make the worktree");

        var start = form.Start(background: false, Token);
        var used = await form.Press("Escape", ctrl: false, shift: false, Token);
        var whileStarting = form.IsOpen;
        _route.Release();
        await start;

        Assert.Equal((true, true, true, "git could not make the worktree"), (used, whileStarting, form.IsOpen, form.FootError));
    }

    [Fact]
    public async Task Opening_the_form_again_after_a_refused_start_still_shows_the_error()
    {
        var form = Form();
        _route.Refuse("SND-AGENT-012", "sindri: the folder is gone");
        await form.Start(background: false, Token);
        form.Close();

        form.Open();

        Assert.Equal(("sindri: the folder is gone", true), (form.RepoError, form.IsOpen));
    }

    [Fact]
    public async Task A_background_start_puts_focus_back_on_Prompt_and_the_next_Ctrl_Enter_reaches_the_form()
    {
        var form = Form();
        for (var i = 0; i < 4; i++)
        {
            form.MoveFocus(back: false);
        }

        _ = await form.Press("Enter", ctrl: true, shift: true, Token);
        var focused = form.Focused;
        form.Prompt = "second";
        _ = await form.Press("Enter", ctrl: true, shift: false, Token);

        Assert.Equal((NewAgentField.Prompt, 2, false), (focused, _route.Sent.Count, form.IsOpen));
    }

    [Fact]
    public void The_form_offers_Claude_Code_and_Codex_only_in_v1()
    {
        var form = Form();

        Assert.Equal(["claude-code", "codex"], form.Agents.Select(a => a.Id), StringComparer.Ordinal);
    }
}
