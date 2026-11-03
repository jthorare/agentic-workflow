using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view's filter run count (#2008): a run is counted as finished only once its rows are showing, so a reader that sees the count go up never reads the old list.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapFilterRunsTests
{
    private const int Entries = 100;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (FileMapViewModel Files, FakeTimeProvider Clock) Small()
    {
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        var entries = new JsonArray();
        for (var i = 0; i < Entries; i++)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"{(i % 10 == 0 ? "alpha" : "beta")}{i}.ts");
            entries.Add(new JsonObject
            {
                ["name"] = name,
                ["path"] = "src/tabs/" + name,
                ["type"] = "file",
                ["status"] = "unmapped",
                ["files"] = 1,
                ["features"] = new JsonArray(),
                ["intent"] = null,
            });
        }

        body["entries"] = entries;
        var api = new FakeSindriApi { FileMapAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString()) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return (new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { })), clock);
    }

    [Fact]
    public async Task A_filter_run_is_counted_only_after_its_rows_are_showing()
    {
        var (files, clock) = Small();
        await files.Load("src/tabs/", Token);

        var runsWhenRowsChanged = new List<int>();
        files.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileMapViewModel.Rows))
            {
                runsWhenRowsChanged.Add(files.FilterRuns);
            }
        };

        files.FilterText = "alpha";
        clock.Advance(FileMapViewModel.FilterDelay);
        await Settled(files, 1);

        Assert.Equal(Entries / 10, files.Rows.Count);
        Assert.NotEmpty(runsWhenRowsChanged);
        Assert.All(runsWhenRowsChanged, runs => Assert.Equal(0, runs));
    }

    private static async Task Settled(FileMapViewModel files, int runs)
    {
        for (var i = 0; i < 500 && files.FilterRuns < runs; i++)
        {
            await Task.Delay(10, Token);
        }

        Assert.Equal(runs, files.FilterRuns);
    }
}
