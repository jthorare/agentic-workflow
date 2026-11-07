using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews header's library button and its errors sheet (review page spec §6.1): docs with errors, near misses, unreadable folders, then the served cap line.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsLibraryTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<LibrarySheetViewModel> Opened(string scenario)
    {
        _f.Server.LibraryAnswer = RouteAnswer.Api("library", scenario);
        return await Opened();
    }

    private async Task<LibrarySheetViewModel> Opened()
    {
        var page = await _f.Shown(Token);
        await page.OpenLibrary(Token);
        return page.Library;
    }

    [Fact]
    public async Task The_header_button_opens_the_sheet_and_reads_the_library()
    {
        var page = await _f.Shown(Token);
        Assert.True(page.CanOpenLibrary);
        Assert.False(page.Library.IsOpen);

        await page.OpenLibrary(Token);

        Assert.Equal((true, false, 1), (page.Library.IsOpen, page.Library.IsLoading, _f.Server.LibraryCalls));
    }

    [Fact]
    public async Task Closing_the_sheet_hides_it()
    {
        var sheet = await Opened("ok");
        Assert.True(sheet.IsOpen);

        sheet.Close();

        Assert.False(sheet.IsOpen);
    }

    [Theory]
    [InlineData("ok", "41 docs")]
    [InlineData("with-errors", "41 docs, 3 with errors")]
    [InlineData("root-unreadable", "33 docs, 1 folder can't be read")]
    public async Task The_summary_reads_the_counts_per_golden(string scenario, string summary)
    {
        var sheet = await Opened(scenario);

        Assert.Equal(summary, sheet.Summary);
    }

    [Fact]
    public async Task Docs_with_errors_list_each_doc_its_path_and_every_error()
    {
        var sheet = await Opened("with-errors");

        var section = Assert.Single(sheet.Sections);
        Assert.Equal(("Docs with errors", 3), (section.Title, section.Count));
        Assert.Equal(["booking-retry-tdd", "queue-sizing-spec", "export-plan"], section.Items.Select(i => i.Name), StringComparer.Ordinal);
        var plan = section.Items[2];
        Assert.Equal("~/notes/design/export-plan.md", plan.Path);
        Assert.Equal(2, plan.Lines.Count);
        Assert.StartsWith("SND-LIB-015: ~/notes/design/export-plan.md line 31", plan.Lines[0], StringComparison.Ordinal);
        Assert.StartsWith("SND-LIB-016: ", plan.Lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Near_misses_alone_make_one_section_with_the_rule_and_the_fix()
    {
        var sheet = await Opened("near-misses-only");

        var section = Assert.Single(sheet.Sections);
        Assert.Equal(("Near misses", 3), (section.Title, section.Count));
        Assert.Equal("~/notes/design/queue-sizing.md", section.Items[0].Name);
        Assert.StartsWith("SND-LIB-019: ", section.Items[0].Lines.Single(), StringComparison.Ordinal);
        Assert.False(sheet.IsClean);
    }

    [Fact]
    public async Task An_unreadable_folder_names_its_reason()
    {
        var sheet = await Opened("root-unreadable");

        var section = Assert.Single(sheet.Sections);
        Assert.Equal("Folders that can't be read", section.Title);
        Assert.Equal(("~/notes/vault", "permission denied"), (section.Items[0].Name, section.Items[0].Lines.Single()));
    }

    [Fact]
    public async Task The_sections_come_in_the_order_docs_near_misses_folders()
    {
        var all = JsonNode.Parse(GoldenFiles.Api("library", "with-errors"))!;
        all["nearMisses"] = JsonNode.Parse(GoldenFiles.Api("library", "near-misses-only"))!["nearMisses"]!.DeepClone();
        all["unreadableRoots"] = JsonNode.Parse(GoldenFiles.Api("library", "root-unreadable"))!["unreadableRoots"]!.DeepClone();
        _f.Server.LibraryAnswer = new RouteAnswer(HttpStatusCode.OK, all.ToJsonString());

        var sheet = await Opened();

        Assert.Equal(["Docs with errors", "Near misses", "Folders that can't be read"], sheet.Sections.Select(s => s.Title), StringComparer.Ordinal);
        Assert.Equal([3, 3, 1], sheet.Sections.Select(s => s.Count));
    }

    [Fact]
    public async Task A_clean_library_says_there_is_nothing_to_fix()
    {
        var sheet = await Opened("ok");

        Assert.Empty(sheet.Sections);
        Assert.True(sheet.IsClean);
        Assert.Equal("Nothing to fix.", Labels.LibraryClean);
        Assert.Null(sheet.CappedText);
    }

    [Fact]
    public async Task A_capped_list_ends_with_the_served_text()
    {
        var sheet = await Opened("capped");

        Assert.Equal("Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest", sheet.CappedText);
        Assert.False(sheet.IsClean);
    }

    [Fact]
    public async Task A_failed_read_says_so_and_shows_no_sections()
    {
        _f.Server.LibraryAnswer = RouteAnswer.Refused("nope");

        var sheet = await Opened();

        Assert.False(sheet.IsLoading);
        Assert.Contains("nope", sheet.Error, StringComparison.Ordinal);
        Assert.Empty(sheet.Sections);
    }
}
