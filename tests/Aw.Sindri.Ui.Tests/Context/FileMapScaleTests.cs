using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view on a folder of 30,000 entries (q103): row view models are built as the list scrolls, and the filter runs once per pause in typing.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapScaleTests
{
    private const int Entries = 30_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (FileMapViewModel Files, FakeTimeProvider Clock) Big()
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
    public async Task A_folder_of_thirty_thousand_entries_builds_a_row_only_when_the_list_asks_for_it()
    {
        var (files, _) = Big();

        await files.Load("src/tabs/", Token);

        Assert.Equal(Entries, files.Rows.Count);
        var rows = Assert.IsType<LazyFileRows>(files.Rows);
        Assert.Equal(0, rows.Built);
        Assert.Equal("alpha0.ts", files.Rows[0].Name);
        Assert.Equal("beta29999.ts", files.Rows[Entries - 1].Name);
        Assert.Equal(2, rows.Built);
    }

    [Fact]
    public async Task The_filter_narrows_the_flat_list_and_clearing_it_brings_every_entry_back()
    {
        var (files, clock) = Big();
        await files.Load("src/tabs/", Token);

        files.FilterText = "ALPHA";
        clock.Advance(FileMapViewModel.FilterDelay);
        await Settled(files, 1);

        Assert.Equal(Entries / 10, files.Rows.Count);
        Assert.All(files.Rows.Take(50), r => Assert.StartsWith("alpha", r.Name, StringComparison.Ordinal));

        files.FilterText = string.Empty;
        clock.Advance(FileMapViewModel.FilterDelay);
        await Settled(files, 2);

        Assert.Equal(Entries, files.Rows.Count);
    }

    [Fact]
    public async Task Rapid_keystrokes_run_the_filter_once_after_the_pause()
    {
        var (files, clock) = Big();
        await files.Load("src/tabs/", Token);

        foreach (var text in new[] { "a", "al", "alp", "alph", "alpha1" })
        {
            files.FilterText = text;
            clock.Advance(TimeSpan.FromMilliseconds(50));
        }

        Assert.Equal(0, files.FilterRuns);
        Assert.Equal(Entries, files.Rows.Count);

        clock.Advance(FileMapViewModel.FilterDelay);
        await Settled(files, 1);

        Assert.Equal(1, files.FilterRuns);
        Assert.All(files.Rows.Take(50), r => Assert.StartsWith("alpha1", r.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_folder_read_while_a_filter_is_set_shows_only_what_matches()
    {
        var (files, clock) = Big();
        await files.Load("src/tabs/", Token);
        files.FilterText = "beta2999";
        clock.Advance(FileMapViewModel.FilterDelay);
        await Settled(files, 1);

        await files.Load("src/tabs/", Token);

        Assert.Equal(10, files.Rows.Count);
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
