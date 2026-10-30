using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Run;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The run page's end card in a real window (run canvas spec §8.5): Run again is a button there when the run ended and the server lists the command, and it is not there otherwise.</summary>
[Lane(TestLane.Integration)]
public sealed class RunAgainViewTests : FrameHost
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // The line stopped at plan and ended at "stuck", as the run page tests have it.
    private static InstanceDetail Ended(params string[] actions)
    {
        var line = RunFixtures.Line("plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = "FAILED" });
        return line with
        {
            State = "stuck",
            Since = A.Noon.AddHours(-1),
            States = [.. line.States.Select(c => string.Equals(c.State, "stuck", StringComparison.Ordinal) ? c with { Glyph = StateGlyph.Done, Outcome = "failed" } : c)],
            Actions = actions,
        };
    }

    private async Task<Window> Shown(InstanceDetail line)
    {
        var body = new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(line, SindriApiClient.Json));
        Server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? body
            : new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)), SindriApiClient.Json));
        var catalog = new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        catalog.Show(_api, Server, (await Server.Commands(_api, Token)).Value);
        var page = new RunPageViewModel(Server, catalog, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        var window = new Window { Width = 1200, Height = 800, Content = new RunPage { DataContext = page } };
        window.Show();
        Settle(window);
        return window;
    }

    [Fact]
    public Task The_end_card_has_a_Run_again_button_when_the_server_lists_it() => OnUi(async () =>
    {
        var window = await Shown(Ended("RerunInstance"));

        Assert.True(Visible(window, "RunEnding") && Visible(window, "RunRerunPanel"));
        Assert.Contains(Named<Control>(window, "RunRerun").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && string.Equals(t.Text, "Run again", StringComparison.Ordinal));
        window.Close();
    });

    [Fact]
    public Task The_end_card_has_no_Run_again_button_when_the_server_does_not_list_it() => OnUi(async () =>
    {
        var window = await Shown(Ended());

        Assert.True(Visible(window, "RunEnding"));
        Assert.False(Visible(window, "RunRerunPanel"));
        window.Close();
    });
}
