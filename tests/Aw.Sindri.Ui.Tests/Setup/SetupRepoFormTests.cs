using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The repo step's form: a folder to type or Browse for, one press that adds it once, its refusals under the field in words, and the terminal command until the catalog says the app may add.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupRepoFormTests
{
    private readonly SetupFixture _f = new("extra-setup-ready");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(SetupViewModel Setup, SetupRepoFormViewModel Form, SetupStepViewModel Step)> Open(SetupFixture? f = null)
    {
        f ??= _f;
        f.Serve("first-run");
        var setup = await f.Read(Token);
        var step = setup.Steps.Single(s => s.Id == "repo");
        return (setup, step.Repo!, step);
    }

    [Fact]
    public async Task The_form_replaces_the_terminal_command_once_the_catalog_lets_the_app_add_a_repo()
    {
        var (_, form, step) = await Open();
        var (_, closed, closedStep) = await Open(new SetupFixture("extra-approve-all-ready"));   // this catalog has no AddRepo row (the served one is ready since api #528)

        Assert.Equal((true, false), (form.IsAvailable, step.HasAction));
        Assert.Equal((false, "sindri-app repo add <path>"), (closed.IsAvailable, closedStep.Action?.Cli?.Cli));
    }

    [Fact]
    public async Task Browse_puts_the_chosen_folder_in_the_field_and_no_choice_leaves_what_was_typed()
    {
        var (_, form, _) = await Open();
        _f.Folders.Queue("/work/demo");
        _f.Folders.Queue(folder: null);

        await form.Browse(Token);
        var chosen = form.Path;
        form.Path = "/typed/path";
        await form.Browse(Token);

        Assert.Equal(("/work/demo", "/typed/path", Labels.SetupBrowseTitle), (chosen, form.Path, _f.Folders.Asked[0]));
    }

    [Fact]
    public async Task A_head_with_no_chooser_shows_the_typed_path_only()
    {
        _f.Folders.IsAvailable = false;

        var (_, form, _) = await Open();

        Assert.False(form.CanBrowse);
    }

    [Fact]
    public async Task Add_sends_the_trimmed_path_once_and_the_row_shows_working_until_the_next_read()
    {
        var (setup, form, step) = await Open();
        form.Path = "  /work/demo  ";

        await form.Add(Token);

        Assert.Equal([new AddRepoRequest("/work/demo")], _f.Server.AddedRepos);
        Assert.Equal(("Working", Labels.SetupRepoAdding, false, string.Empty), (step.ChipWord, step.Note, form.HasError, form.Path));
        await setup.Refresh(Url, Token);
        Assert.Equal("Needs you", step.ChipWord);   // the server's word wins again
    }

    [Fact]
    public async Task One_press_sends_one_add_even_when_a_second_follows_before_the_answer()
    {
        var (_, form, _) = await Open();
        form.Path = "/work/demo";
        _f.Server.WriteGate = new TaskCompletionSource();

        var first = form.Add(Token);
        var sending = form.IsSending;
        var second = form.Add(Token);
        _f.Server.WriteGate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal((true, 1, false), (sending, _f.Server.AddedRepos.Count, form.IsSending));
    }

    [Fact]
    public async Task An_empty_field_sends_nothing_and_says_so_under_the_field_with_focus_in_it()
    {
        var (_, form, _) = await Open();
        var asked = new List<SetupFocus>();
        form.FocusRequested += (_, e) => asked.Add(e.Target);
        form.Path = "   ";

        await form.Add(Token);

        Assert.Equal((0, Labels.SetupRepoEmpty), (_f.Server.AddedRepos.Count, form.ErrorText));
        Assert.Equal([SetupFocus.RepoPath], asked);
    }

    [Theory]
    [InlineData("not-a-checkout", "That folder isn't a git checkout, or can't be read.")]
    [InlineData("not-absolute", "That folder isn't a git checkout, or can't be read.")]
    [InlineData("already-added", "That repository is already added, or another repository has its name.")]
    [InlineData("name-taken", "That repository is already added, or another repository has its name.")]
    [InlineData("profile-changed", "The profile changed while Sindri was writing it. Try again.")]
    [InlineData("refused-session", "Sindri's own steps can't answer this.")]
    public async Task A_refusal_is_said_in_words_under_the_field_with_the_path_kept_and_focus_back_in_it(string scenario, string sentence)
    {
        var (_, form, step) = await Open();
        _f.Server.AddRepoAnswer = RouteAnswer.Problem("setup-repos", scenario);
        var asked = new List<SetupFocus>();
        form.FocusRequested += (_, e) => asked.Add(e.Target);
        form.Path = "/work/demo";

        await form.Add(Token);

        Assert.Equal((sentence, "/work/demo", false, "Needs you"), (form.ErrorText, form.Path, form.IsSending, step.ChipWord));
        Assert.Equal([SetupFocus.RepoPath], asked);
        Assert.DoesNotContain("SND-", form.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("fix:", form.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sindri_that_does_not_answer_is_a_plain_sentence_and_typing_clears_it()
    {
        var (_, form, _) = await Open();
        form.Path = "/work/demo";
        _f.Server.GoDown();

        await form.Add(Token);
        var said = form.ErrorText;
        form.Path = "/work/demo2";

        Assert.Equal((Labels.SetupRepoFailed, string.Empty), (said, form.ErrorText));
    }

    [Fact]
    public async Task A_failed_write_reaches_the_row_as_the_servers_own_sentence_with_the_path_and_the_reason()
    {
        const string sentence = "Couldn't write /work/demo to your settings: permission denied.";
        _f.Serve(WithError(With("first-run", "repo", "needs-you", "AddRepo"), "repo", "SND-PROFILE-002", sentence));

        var step = (await _f.Read(Token)).Steps.Single(s => s.Id == "repo");

        Assert.Equal((sentence, true), (step.ErrorText, step.HasError));
    }

    [Fact]
    public async Task A_done_repo_step_offers_another_repo_on_the_same_form()
    {
        _f.Serve("all-done");

        var form = (await _f.Read(Token)).Steps.Single(s => s.Id == "repo").Repo!;

        Assert.Equal("Add another repo", form.AddText);
    }

    [Fact]
    public async Task The_Add_button_waits_for_a_path_and_for_the_answer()
    {
        var (_, form, _) = await Open();

        var empty = form.CanAdd;
        form.Path = "/work/demo";
        var typed = form.CanAdd;

        Assert.Equal((false, true), (empty, typed));
    }

    [Fact]
    public async Task A_step_button_never_sends_a_protected_approval()
    {
        _f.Serve(With(With("all-done", "profile", "needs-you", "ApproveProfile"), "recipes", "needs-you", "ApproveAllRecipes"));
        var setup = await _f.Read(Token);

        foreach (var step in setup.Steps.Where(s => s.Action is not null))
        {
            step.Action!.Open();
        }

        Assert.Equal((0, 0, 0, 0, 0), (_f.Server.AddedRepos.Count, _f.Server.RegisteredMcp.Count, _f.Server.SetupOnyxCalls, _f.Server.OnyxLogins.Count, _f.Server.ApproveAllJobCalls));
        Assert.Equal(0, _f.Server.RefreshContextMapCalls);
    }

    private static System.Text.Json.Nodes.JsonNode WithError(System.Text.Json.Nodes.JsonNode list, string step, string code, string sentence)
    {
        var served = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!;
        served["error"] = new System.Text.Json.Nodes.JsonObject { ["code"] = code, ["sentence"] = sentence };
        return list;
    }
}
