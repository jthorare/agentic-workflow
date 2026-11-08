using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The quick-add box (todo spec §3.2, §3.3, §3.4): the preview line, Enter, a failed save and its retry, Esc, and the keys that open it.</summary>
[Lane(TestLane.Unit)]
public sealed class QuickAddTests : IDisposable
{
    private static readonly Uri _url = new("http://127.0.0.1:7410/");

    private readonly InlineUiContext _ui = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-11-08T12:00:00Z", CultureInfo.InvariantCulture));   // a Sunday

    public void Dispose() => _ui.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private QuickAddViewModel Box(Uri? url = null)
    {
        var box = new QuickAddViewModel(_server, () => url ?? _url, _clock);
        box.Open();
        return box;
    }

    private TodoAdd Sent(int index = 0) => Assert.IsType<TodoAdd>(_server.TodoWrites[index].Body);

    private async Task<ShellViewModel> Running()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var shell = new ShellViewModel(records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.State == ShellState.Normal);
        return shell;
    }

    private async Task<ShellViewModel> Stopped()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.State == ShellState.NotRunning);
        return shell;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public void The_line_under_the_field_says_what_will_be_saved()
    {
        var box = Box();
        Assert.Equal("No date", box.Preview);

        box.Text = "check flaky test fri";
        Assert.Equal("Due Fri 11-13", box.Preview);

        box.Text = "check flaky test fri\"";
        Assert.Equal("No date", box.Preview);

        box.Text = "call the dentist";
        Assert.Equal("No date", box.Preview);
    }

    [Fact]
    public async Task Enter_saves_the_text_without_its_day_word_and_sends_the_day_then_closes()
    {
        var box = Box();
        box.Text = "reply to Sam about the offsite fri";

        await box.Save(Token);

        var add = Sent();
        Assert.Equal(("reply to Sam about the offsite", "2026-11-13", "/sindri/todos"), (add.Text, add.Due, _server.TodoWrites[0].Route));
        Assert.Null(add.Links);
        Assert.NotEmpty(add.ClientId);
        Assert.Equal((false, string.Empty, string.Empty), (box.IsOpen, box.Text, box.Error));
    }

    [Fact]
    public async Task A_todo_with_no_day_sends_an_empty_due()
    {
        var box = Box();
        box.Text = "call the dentist";

        await box.Save(Token);

        Assert.Equal(("call the dentist", string.Empty), (Sent().Text, Sent().Due));
    }

    [Fact]
    public async Task Enter_on_empty_text_says_so_sends_nothing_and_stays_open_until_the_next_key()
    {
        var box = Box();
        box.Text = "   ";

        await box.Save(Token);

        Assert.Equal(("Type a todo first", true, 0), (box.Preview, box.IsOpen, _server.TodoWrites.Count));

        box.Text = "x";
        Assert.Equal("No date", box.Preview);
    }

    [Fact]
    public async Task A_refused_save_keeps_the_box_open_with_the_text_and_the_servers_reason()
    {
        _server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-TODO-007", "A todo can't contain hidden or control characters.");
        var box = Box();
        box.Text = "bad text fri";

        await box.Save(Token);

        Assert.Equal((true, "bad text fri", "A todo can't contain hidden or control characters.", true, false), (box.IsOpen, box.Text, box.Error, box.HasError, box.IsSaving));
    }

    [Fact]
    public async Task A_save_nothing_answered_says_sindri_is_not_answering_and_keeps_the_text()
    {
        _server.GoDown();
        var box = Box();
        box.Text = "call the dentist";

        await box.Save(Token);

        Assert.Equal((true, "call the dentist", "Sindri isn't answering"), (box.IsOpen, box.Text, box.Error));
    }

    [Fact]
    public async Task With_no_address_the_save_says_sindri_is_not_answering_and_sends_nothing()
    {
        var box = new QuickAddViewModel(_server, () => null, _clock);
        box.Open();
        box.Text = "x";

        await box.Save(Token);

        Assert.Equal(("Sindri isn't answering", 0), (box.Error, _server.TodoWrites.Count));
    }

    [Fact]
    public async Task Enter_retries_with_the_same_client_id_and_success_closes_the_box()
    {
        _server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");
        var box = Box();
        box.Text = "rotate the key";
        await box.Save(Token);
        _server.TodoWriteAnswer = null;

        await box.Save(Token);

        Assert.Equal((2, true, false, string.Empty), (_server.TodoWrites.Count, Sent(0).ClientId == Sent(1).ClientId, box.IsOpen, box.Error));
    }

    [Fact]
    public async Task Editing_the_text_after_a_failure_clears_the_reason_and_makes_a_new_client_id()
    {
        _server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");
        var box = Box();
        box.Text = "one";
        await box.Save(Token);
        box.Text = "two";
        Assert.Equal(string.Empty, box.Error);
        _server.TodoWriteAnswer = null;

        await box.Save(Token);

        Assert.NotEqual(Sent(0).ClientId, Sent(1).ClientId, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Esc_closes_the_box_and_drops_the_text_without_sending()
    {
        var box = Box();
        box.Text = "never mind";

        box.Close();

        Assert.Equal((false, string.Empty, "No date", 0), (box.IsOpen, box.Text, box.Preview, _server.TodoWrites.Count));
        await box.Save(Token);
        Assert.Empty(_server.TodoWrites);
    }

    [Fact]
    public void Opening_again_starts_with_an_empty_field_but_an_open_box_keeps_its_text()
    {
        var box = Box();
        box.Text = "half typed";
        box.Open();
        Assert.Equal("half typed", box.Text);

        box.Close();
        box.Open();
        Assert.Equal(string.Empty, box.Text);
    }

    [Fact]
    public async Task N_opens_the_box()
    {
        var shell = await Running();

        Assert.True(shell.PressQuickAdd());

        Assert.True(shell.QuickAdd.IsOpen);
    }

    [Fact]
    public async Task A_form_held_open_takes_neither_key()
    {
        var shell = await Running();
        shell.Create.Open();

        Assert.False(shell.PressQuickAdd());
        Assert.False(shell.PressQuickAdd());

        Assert.False(shell.QuickAdd.IsOpen);
    }

    [Fact]
    public async Task The_chord_closes_the_palette_and_the_shortcuts_sheet_first()
    {
        var shell = await Running();
        shell.Palette.Open();
        shell.Shortcuts.Open();

        Assert.True(shell.PressQuickAdd());

        Assert.Equal((false, false, true), (shell.Palette.IsOpen, shell.Shortcuts.IsOpen, shell.QuickAdd.IsOpen));
    }

    [Fact]
    public async Task While_sindri_is_not_running_neither_key_opens_the_box_and_the_hook_is_told()
    {
        var shell = await Stopped();
        var told = 0;
        shell.QuickAddWhileStopped += (_, _) => told++;

        Assert.True(shell.PressQuickAdd());
        Assert.True(shell.PressQuickAdd());

        Assert.Equal((false, 2), (shell.QuickAdd.IsOpen, told));
    }

    [Fact]
    public async Task The_palette_has_a_New_todo_entry_that_opens_the_box_and_closes_the_palette()
    {
        var shell = await Running();
        shell.Palette.Open();
        var entry = Assert.Single(shell.Palette.Entries, e => string.Equals(e.Label, "New todo", StringComparison.Ordinal));

        await shell.Palette.Run(entry, Token);

        Assert.Equal((true, false), (shell.QuickAdd.IsOpen, shell.Palette.IsOpen));
    }
}
