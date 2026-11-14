using System.Globalization;
using System.Windows.Input;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The made-up rows and groups the RowList tests draw (#2978): five fact columns, some long text, a button on every row.</summary>
internal static class RowGallery
{
    internal static readonly IReadOnlyList<RowColumn> Columns =
    [
        new("status", "Status"),
        new("progress", "Progress"),
        new("diff", "Diff"),
        new("ref", "Branch", Wide: true),
        new("owner", "Owner"),
    ];

    internal const string LongRef = "acme-platform-infrastructure/exports";

    internal static GalleryGroup Group(string heading, int count, int first = 0, Func<int, GalleryRow>? make = null) =>
        new(heading, [.. Enumerable.Range(first, count).Select(i => make?.Invoke(i) ?? Row(i))]);

    internal static GalleryRow Row(int i, string? title = null, string? reference = null, ICommand? action = null) => new(
        Id: "r-" + i.ToString("0000", CultureInfo.InvariantCulture),
        Title: title ?? ("Row " + i.ToString(CultureInfo.InvariantCulture)),
        Facts: new Dictionary<string, RowFact>(StringComparer.Ordinal)
        {
            ["status"] = new(RowFactKind.Chip, "In review"),
            ["progress"] = new(RowFactKind.Progress, "3/8", Done: 3, Total: 8),
            ["diff"] = new(RowFactKind.Diffstat, "+120 -4"),
            ["ref"] = new(RowFactKind.Ref, reference ?? "acme/exports"),
            ["owner"] = new(RowFactKind.Person, "Maya"),
        },
        Age: i % 3 == 0 ? "3d" : "12m",
        Actions: action is null ? [] : [new RowAction("Approve", action)]);

    /// <summary>Three groups of 14 mixed rows: row 1 has a very long title, row 2 a very long ref.</summary>
    internal static List<GalleryGroup> Three() =>
    [
        Group("Needs you", 14, 0, i => i switch
        {
            1 => Row(i, title: "A very long title that cannot fit in the room the list leaves for it, so it has to be cut with an ellipsis and not wrap onto a second line"),
            2 => Row(i, reference: LongRef),
            _ => Row(i),
        }),
        Group("In progress", 14, 100),
        Group("Done this week", 14, 200),
    ];

    internal static List<GalleryGroup> Many(int groups, int perGroup) =>
        [.. Enumerable.Range(0, groups).Select(g => Group("Group " + g.ToString(CultureInfo.InvariantCulture), perGroup, g * perGroup))];
}
