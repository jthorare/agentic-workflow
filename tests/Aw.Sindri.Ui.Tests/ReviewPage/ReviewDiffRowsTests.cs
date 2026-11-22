using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The diff as one flat list of rows (#2776): group, file, hunk and line rows in reading order, the card edges, and range edits on fold and Show.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewDiffRowsTests
{
    private const string TwoHunks = "@@ -1,1 +1,1 @@\n-a = 1;\n+a = 2;\n@@ -10,1 +10,2 @@\n b = 1;\n+c = 1;";

    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The full golden with X.cs given two hunks: Core holds X.cs (2 hunks of 2 lines), Budget.cs (1 hunk, 2 lines), Old.cs (deleted, hidden), 0042_retry.sql
    // (1 hunk, 2 lines); Assets is folded.
    private Task<ReviewPageViewModel> Opened() =>
        _f.Opened("full", json => json["diff"]!["groups"]![0]!["files"]![0]!["patch"] = JsonValue.Create(TwoHunks), Token);

    private static string Shape(ReviewDiffRow row) => row switch
    {
        ReviewDiffGroupRow g => "G " + g.Group.Name,
        ReviewDiffFileRow f => "F " + f.File.Path,
        ReviewDiffHunkRow h => "H " + h.Hunk.Header,
        ReviewDiffLineRow l => "L " + l.Line.Key,
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };

    [Fact]
    public async Task The_rows_are_group_file_hunk_and_line_rows_in_reading_order_and_a_folded_group_is_its_header_only()
    {
        var page = await Opened();

        Assert.Equal(
            [
                "G Core",
                "F src/Api/A/A.Core/X.cs", "H @@ -1,1 +1,1 @@", "L src/Api/A/A.Core/X.cs:-1", "L src/Api/A/A.Core/X.cs:+1", "H @@ -10,1 +10,2 @@", "L src/Api/A/A.Core/X.cs:10", "L src/Api/A/A.Core/X.cs:+11",
                "F src/Api/A/A.Core/Budget.cs", "H @@ -1,2 +1,2 @@", "L src/Api/A/A.Core/Budget.cs:-1", "L src/Api/A/A.Core/Budget.cs:+1",
                "F src/Api/A/A.Core/Old.cs",
                "F src/Api/Migrations/0042_retry.sql", "H @@ -0,0 +1,2 @@", "L src/Api/Migrations/0042_retry.sql:+1", "L src/Api/Migrations/0042_retry.sql:+2",
                "G Assets",
            ],
            page.Diff.Rows.Select(Shape),
            StringComparer.Ordinal);
        Assert.Equal(
            (2, 4, 4, 8),
            (page.Diff.Rows.Count(r => r.Kind == ReviewDiffRowKind.Group), page.Diff.Rows.Count(r => r.Kind == ReviewDiffRowKind.File),
                page.Diff.Rows.Count(r => r.Kind == ReviewDiffRowKind.Hunk), page.Diff.Rows.Count(r => r.Kind == ReviewDiffRowKind.Line)));
    }

    [Fact]
    public async Task Only_each_files_last_row_closes_its_card_and_a_hidden_file_is_its_header_alone()
    {
        var page = await Opened();

        Assert.Equal(
            ["L src/Api/A/A.Core/X.cs:+11", "L src/Api/A/A.Core/Budget.cs:+1", "F src/Api/A/A.Core/Old.cs", "L src/Api/Migrations/0042_retry.sql:+2"],
            page.Diff.Rows.Where(r => r.IsLast).Select(Shape),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Folding_a_group_drops_only_its_rows_and_keeps_its_header_row_and_opening_brings_them_back()
    {
        var page = await Opened();
        var header = page.Diff.Rows[0];
        var assets = page.Diff.Rows[^1];

        page.Diff.Groups[0].IsFolded = true;

        Assert.Equal(["G Core", "G Assets"], page.Diff.Rows.Select(Shape), StringComparer.Ordinal);
        Assert.Same(header, page.Diff.Rows[0]);
        Assert.Same(assets, page.Diff.Rows[1]);

        page.Diff.Groups[0].IsFolded = false;
        page.Diff.Groups[1].IsFolded = false;

        Assert.Equal(19, page.Diff.Rows.Count);   // the 18 rows, plus Assets' binary file: a header with no hunks
        Assert.Equal("F assets/budget.png", Shape(page.Diff.Rows[^1]));
        Assert.True(page.Diff.Rows[^1].IsLast);
    }

    [Fact]
    public async Task Show_on_a_deleted_file_puts_its_lines_under_its_header()
    {
        var page = await Opened();
        var deleted = page.Diff.Groups[0].Files.Single(f => f.HasShow);

        deleted.Show();

        var at = page.Diff.RowOf(ReviewFocusRegion.FileNote, deleted.Path);
        Assert.Equal(
            ["F src/Api/A/A.Core/Old.cs", "H @@ -1,3 +0,0 @@", "L src/Api/A/A.Core/Old.cs:-1", "L src/Api/A/A.Core/Old.cs:-2", "L src/Api/A/A.Core/Old.cs:-3", "F src/Api/Migrations/0042_retry.sql"],
            page.Diff.Rows.Skip(at).Take(6).Select(Shape),
            StringComparer.Ordinal);
        Assert.Equal((false, true), (page.Diff.Rows[at].IsLast, page.Diff.Rows[at + 4].IsLast));
    }

    [Fact]
    public async Task RowOf_finds_a_line_by_key_a_file_by_path_and_a_group_by_name_and_minus_one_for_a_folded_line()
    {
        var page = await Opened();

        Assert.Equal(
            (4, 8, 17, -1),
            (page.Diff.RowOf(ReviewFocusRegion.DiffLine, "src/Api/A/A.Core/X.cs:+1"), page.Diff.RowOf(ReviewFocusRegion.FileNote, "src/Api/A/A.Core/Budget.cs"),
                page.Diff.RowOf(ReviewFocusRegion.GroupHeader, "Assets"), page.Diff.RowOf(ReviewFocusRegion.FileNote, "assets/budget.png")));
    }
}
