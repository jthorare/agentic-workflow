using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Todo.AttentionFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The shell's side of the attention rows (todo spec §3.3, D11): a pick's toast counts down from 10 and queues the toasts that come after it.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellAttentionTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        var todos = new JsonArray(TextOnly("td_c1", "reply to Sam"), TodoFixture.Open("td_a", "first"));
        var list = TodoFixture.Golden();
        list["todos"] = todos;
        _server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSample.WithConflicts("td_c1"));
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.Todo.Attention is not null, Token);
        _clock.Advance(TimeSpan.FromSeconds(1));   // past the quiet window of a row that arrived with the first read
        return shell;
    }

    [Fact]
    public Task A_pick_shows_a_counting_toast_with_Undo_ten_seconds() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        await shell.Todo.Pick(2);

        Assert.True(shell.Toast.IsPickOpen);
        Assert.Equal(("Kept Obsidian's version.", "Undo (10 s)"), (shell.Toast.Text, shell.Toast.UndoLabel));
    }, Token);

    [Fact]
    public Task A_toast_that_comes_after_a_pick_waits_behind_it() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        await shell.Todo.Pick(1);

        await shell.Todo.Toggle("td_a");

        Assert.Equal("Kept Sindri's version.", shell.Toast.Text);
        Assert.Equal(1, shell.Toast.PendingCount);
    }, Token);
}
