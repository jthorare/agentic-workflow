using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view reading a folder in cursor pages (q103): the first page at once, the next ahead of the scroll, placeholders for rows not read yet.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapPagingTests
{
    private const int Total = 500;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Name(int i) => string.Create(CultureInfo.InvariantCulture, $"f{i}.ts");

    // A folder of Total files served in pages of the limit asked for; the cursor is the last entry's path.
    private static RouteAnswer Page(FileMapQuery query, string folder, bool pages)
    {
        var from = query.Cursor is { } cursor ? int.Parse(cursor[(folder.Length + 1)..^3], CultureInfo.InvariantCulture) + 1 : 0;
        var to = pages ? Math.Min(Total, from + (query.Limit ?? 200)) : Total;
        var entries = new JsonArray();
        for (var i = from; i < to; i++)
        {
            entries.Add(new JsonObject
            {
                ["name"] = Name(i),
                ["path"] = folder + Name(i),
                ["type"] = "file",
                ["status"] = "unmapped",
                ["files"] = 1,
                ["features"] = new JsonArray(),
                ["intent"] = null,
            });
        }

        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["path"] = folder;
        body["entries"] = entries;
        if (pages)
        {
            body["nextCursor"] = to < Total ? folder + Name(to - 1) : null;
            body["total"] = Total;
        }

        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private static (FileMapViewModel Files, FakeSindriApi Api, FakeTimeProvider Clock) Paged(bool pages = true)
    {
        var api = new FakeSindriApi { FileMapResponder = q => Page(q, q.Path ?? string.Empty, pages) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return (new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { })), api, clock);
    }

    private static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            await Task.Delay(10, Token);
        }

        Assert.True(done());
    }

    [Fact]
    public async Task The_first_page_shows_at_once_sized_to_the_total_with_placeholders_for_the_rest()
    {
        var (files, api, _) = Paged();

        await files.Load("src/tabs/", Token);

        Assert.Equal(Total, files.Rows.Count);
        Assert.Equal(200, files.Loaded);
        Assert.Equal(Name(0), files.Rows[0].Name);
        var first = Assert.Single(api.FileMapQueries);   // loading reads one page; only a scroll toward the end reads the next
        Assert.Equal((null, FileMapViewModel.PageSize), (first.Cursor, first.Limit));
        Assert.Equal(Labels.ContextFilesLoading, files.Rows[300].Name);   // asking for a row past what is read is a scroll there
        Assert.False(files.Rows[300].IsDirectory);
    }

    [Fact]
    public async Task Three_pages_chain_through_their_cursors_as_the_list_scrolls_to_the_end()
    {
        var (files, api, _) = Paged();
        await files.Load("src/tabs/", Token);

        _ = files.Rows[160];
        await Until(() => files.Loaded == 400);
        _ = files.Rows[360];
        await Until(() => files.Loaded == Total);

        Assert.Equal([null, "src/tabs/" + Name(199), "src/tabs/" + Name(399)], api.FileMapQueries.Select(q => q.Cursor), StringComparer.Ordinal);
        Assert.Equal(Total, files.Rows.Count);
        Assert.Equal(Name(450), files.Rows[450].Name);
        Assert.Equal(3, api.FileMapQueries.Count);
    }

    [Fact]
    public async Task The_next_page_is_read_only_when_the_list_comes_within_fifty_rows_of_what_is_read()
    {
        var (files, api, _) = Paged();
        await files.Load("src/tabs/", Token);

        _ = files.Rows[100];
        _ = files.Rows[149];
        _ = Assert.Single(api.FileMapQueries);

        _ = files.Rows[150];
        Assert.Equal(2, api.FileMapQueries.Count);
        await Until(() => files.Loaded == 400);
    }

    [Fact]
    public async Task A_page_that_arrives_after_the_folder_changed_is_dropped()
    {
        var (files, api, _) = Paged();
        await files.Load("src/tabs/", Token);
        var gate = new TaskCompletionSource();
        api.FileMapGate = gate;
        _ = files.Rows[190];
        Assert.Equal(2, api.FileMapQueries.Count);
        api.FileMapGate = null;

        await files.Load("src/other/", Token);
        gate.SetResult();
        await Task.Delay(50, Token);

        Assert.Equal("src/other/", files.PathText);
        Assert.Equal(200, files.Loaded);
        Assert.StartsWith("src/other/", files.Rows[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_that_does_not_page_still_shows_the_full_list()
    {
        var (files, api, _) = Paged(pages: false);

        await files.Load("src/tabs/", Token);

        Assert.Equal(Total, files.Rows.Count);
        Assert.All(files.Rows, r => Assert.False(string.Equals(Labels.ContextFilesLoading, r.Name, StringComparison.Ordinal)));
        _ = Assert.Single(api.FileMapQueries);
    }

    [Fact]
    public async Task A_filter_reads_the_rest_of_the_folder_and_covers_all_of_it_when_it_has_arrived()
    {
        var (files, api, clock) = Paged();
        await files.Load("src/tabs/", Token);

        files.FilterText = "f49";
        clock.Advance(FileMapViewModel.FilterDelay);
        await Until(() => files.Loaded == Total && files.Rows.Count == 11 && string.Equals(files.Note, Labels.ContextMapNamesDetail, StringComparison.Ordinal));

        Assert.Equal(3, api.FileMapQueries.Count);
        Assert.All(files.Rows, r => Assert.Contains("f49", r.Name, StringComparison.Ordinal));
    }
}
