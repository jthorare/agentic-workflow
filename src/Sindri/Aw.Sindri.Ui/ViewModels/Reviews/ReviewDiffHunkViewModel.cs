namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One hunk of a file's patch (review page spec §6.4): its "@@" header, its range read aloud as "lines 10 to 15", and its lines.</summary>
internal sealed class ReviewDiffHunkViewModel : ObservableObject
{
    internal ReviewDiffHunkViewModel(string header, int from, int to, IReadOnlyList<ReviewDiffLineViewModel> lines)
    {
        (Header, Lines) = (header, lines);
        AutomationName = Labels.ReviewHunkName(from, to);
    }

    /// <summary>The "@@ -10,3 +10,6 @@" line as the patch has it.</summary>
    public string Header { get; }

    /// <summary>"lines 10 to 15": the header's name.</summary>
    public string AutomationName { get; }

    /// <summary>The hunk's lines.</summary>
    public IReadOnlyList<ReviewDiffLineViewModel> Lines { get; }
}
