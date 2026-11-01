namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One section of an action's or page's node panel (pages spec §3.4): a heading over link rows, or its loading, empty or failure line.</summary>
internal sealed class NodeSectionViewModel
{
    private NodeSectionViewModel(string heading, IReadOnlyList<NodeEdgeRowViewModel> rows, string emptyText, string mark, string failedText, bool isLoading)
    {
        (Heading, Rows, EmptyText, Mark, FailedText, IsLoading) = (heading, rows, emptyText, mark, failedText, isLoading);
    }

    /// <summary>Gets the heading ("Relates to").</summary>
    public string Heading { get; }

    /// <summary>Gets the link rows; empty while loading, failed or empty.</summary>
    public IReadOnlyList<NodeEdgeRowViewModel> Rows { get; }

    /// <summary>Gets the mark shown on every row ("Code found"); empty when the section has none.</summary>
    public string Mark { get; }

    /// <summary>Gets the line shown when the section has no rows and did not fail.</summary>
    public string EmptyText { get; }

    /// <summary>Gets the failure line; empty unless failed.</summary>
    public string FailedText { get; }

    /// <summary>Gets a value indicating whether the section's read is still running.</summary>
    public bool IsLoading { get; }

    /// <summary>Gets the line while the section's read runs.</summary>
    public string LoadingText { get; } = Labels.ContextNodePanelLoading;

    /// <summary>Gets a value indicating whether the section failed.</summary>
    public bool IsFailed => FailedText.Length > 0;

    /// <summary>Gets a value indicating whether the empty line shows.</summary>
    public bool IsEmpty => !IsLoading && !IsFailed && Rows.Count == 0;

    /// <summary>Gets a value indicating whether the rows show.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Gets a value indicating whether the rows carry a mark.</summary>
    public bool HasMark => Mark.Length > 0;

    /// <summary>A section with its rows, or its empty line when there are none.</summary>
    internal static NodeSectionViewModel Of(string heading, IReadOnlyList<NodeEdgeRowViewModel> rows, string emptyText, string mark = "") => new(heading, rows, emptyText, mark, string.Empty, isLoading: false);

    /// <summary>A section whose read is still running.</summary>
    internal static NodeSectionViewModel Loading(string heading) => new(heading, [], string.Empty, string.Empty, string.Empty, isLoading: true);

    /// <summary>A section whose read failed.</summary>
    internal static NodeSectionViewModel Failed(string heading, string failedText) => new(heading, [], string.Empty, string.Empty, failedText, isLoading: false);
}
