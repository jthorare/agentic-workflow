#pragma warning disable MA0041, CA1822 // red-first stubs: they throw until the implementation commit

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One section of an action's or page's node panel (pages spec §3.4): a heading over link rows, or its empty line, or the failure line.</summary>
internal sealed class NodeSectionViewModel
{
    /// <summary>Gets the heading ("Relates to").</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>Gets the link rows; empty while loading, failed or empty.</summary>
    public IReadOnlyList<NodeEdgeRowViewModel> Rows => throw new NotSupportedException();

    /// <summary>Gets the mark shown on every row ("Code found"); empty when the section has none.</summary>
    public string Mark => throw new NotSupportedException();

    /// <summary>Gets the line shown when the section has no rows and did not fail.</summary>
    public string EmptyText => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the empty line shows.</summary>
    public bool IsEmpty => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the section's own read failed.</summary>
    public bool IsFailed => throw new NotSupportedException();

    /// <summary>Gets the failure line; empty unless failed.</summary>
    public string FailedText => throw new NotSupportedException();
}
