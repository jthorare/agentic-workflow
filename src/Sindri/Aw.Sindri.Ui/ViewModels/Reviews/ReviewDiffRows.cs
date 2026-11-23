namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Builds the flat row list from the groups (#2776).</summary>
internal static class ReviewDiffRows
{
    /// <summary>The rows in reading order: each group's header, then, when the group is open, each file's header, its hunk headers and their lines.</summary>
    /// <param name="groups">The groups, in order.</param>
    /// <returns>The rows.</returns>
    internal static List<ReviewDiffRow> Of(IReadOnlyList<ReviewDiffGroupViewModel> groups)
    {
        var rows = new List<ReviewDiffRow>();
        foreach (var group in groups)
        {
            rows.Add(new ReviewDiffGroupRow(group));
            rows.AddRange(Body(group));
        }

        return rows;
    }

    /// <summary>The rows under a group's header: none while it is folded, else each file's rows.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The rows.</returns>
    internal static List<ReviewDiffRow> Body(ReviewDiffGroupViewModel group)
    {
        var rows = new List<ReviewDiffRow>();
        if (!group.IsFolded)
        {
            foreach (var file in group.Files)
            {
                AddFile(rows, file);
            }
        }

        return rows;
    }

    /// <summary>One file's rows: its header, then each hunk's header and lines.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The rows.</returns>
    internal static List<ReviewDiffRow> File(ReviewDiffFileViewModel file)
    {
        var rows = new List<ReviewDiffRow>();
        AddFile(rows, file);
        return rows;
    }

    /// <summary>The index of the row a focus key names: a line by its key, a file header by its path, a group header by its name; -1 when no row has it.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="region">The diff line, file note or group header region.</param>
    /// <param name="key">The key.</param>
    /// <returns>The index or -1.</returns>
    internal static int IndexOf(IReadOnlyList<ReviewDiffRow> rows, ReviewFocusRegion region, string key)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (Names(rows[i], region, key))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Names(ReviewDiffRow row, ReviewFocusRegion region, string key) => (region, row) switch
    {
        (ReviewFocusRegion.DiffLine, ReviewDiffLineRow line) => string.Equals(line.Line.Key, key, StringComparison.Ordinal),
        (ReviewFocusRegion.FileNote, ReviewDiffFileRow file) => string.Equals(file.File.Path, key, StringComparison.Ordinal),
        (ReviewFocusRegion.GroupHeader, ReviewDiffGroupRow group) => string.Equals(group.Group.Name, key, StringComparison.Ordinal),
        _ => false,
    };

    private static void AddFile(List<ReviewDiffRow> rows, ReviewDiffFileViewModel file)
    {
        var hunks = file.Hunks;
        rows.Add(new ReviewDiffFileRow(file, isLast: hunks.Count == 0));
        for (var h = 0; h < hunks.Count; h++)
        {
            var lastHunk = h == hunks.Count - 1;
            var lines = hunks[h].Lines;
            rows.Add(new ReviewDiffHunkRow(hunks[h], isLast: lastHunk && lines.Count == 0));
            for (var l = 0; l < lines.Count; l++)
            {
                rows.Add(new ReviewDiffLineRow(lines[l], isLast: lastHunk && l == lines.Count - 1));
            }
        }
    }
}
