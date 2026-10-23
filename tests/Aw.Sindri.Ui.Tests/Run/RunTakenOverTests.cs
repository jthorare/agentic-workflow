using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;
using StateGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>Who took a run over and when (api #754): "Taken over by you at 11:04" in the run header.</summary>
[Lane(TestLane.Unit)]
public sealed class RunTakenOverTests
{
    [Theory]
    [InlineData("11:04", "human:cli", "Taken over by you at 11:04")]
    [InlineData("11:04", null, "Taken over at 11:04")]
    [InlineData(null, "human:cli", "Taken over by you")]
    [InlineData(null, null, "")]
    [InlineData("Oct 11, 23:59", "session:abc", "Taken over by an agent session at Oct 11, 23:59")]
    public void A_part_that_is_not_served_is_left_out(string? clock, string? by, string words) => Assert.Equal(words, Labels.RunTakenOver(clock, by));

    [Fact]
    public async Task The_run_header_says_who_took_the_run_over_and_when()
    {
        var line = RunFixtures.Line("plan");
        var taken = line with { Current = line.Current with { Status = line.Current.Status with { TakenOverAt = new DateTimeOffset(2026, 10, 11, 11, 4, 0, TimeSpan.Zero), TakenOverBy = "human:cli" } } };
        var server = new FakeSindriApi
        {
            InstanceBody = id => string.Equals(id, taken.Id, StringComparison.Ordinal)
                ? new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(taken, SindriApiClient.Json))
                : new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)), SindriApiClient.Json)),
        };
        var page = new RunPageViewModel(server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));

        await page.Open(new Uri("http://127.0.0.1:7410/"), taken.Address, taken.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Taken over by you at 11:04", page.TakenOverText);
    }

    [Fact]
    public async Task A_run_nobody_took_over_says_nothing_of_it()
    {
        var line = RunFixtures.Line("plan");
        var server = new FakeSindriApi
        {
            InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal)
                ? new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(line, SindriApiClient.Json))
                : new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)), SindriApiClient.Json)),
        };
        var page = new RunPageViewModel(server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));

        await page.Open(new Uri("http://127.0.0.1:7410/"), line.Address, line.Id, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, page.TakenOverText);
    }
}
