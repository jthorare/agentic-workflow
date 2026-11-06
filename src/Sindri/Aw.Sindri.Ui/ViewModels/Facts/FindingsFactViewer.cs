#pragma warning disable CA1822, MA0041 // a red stub: its members throw until the viewer is written
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A findings fact (<c language="text">fact:findings</c>): a row per rule finding; a body that is not a findings list is drawn as the payload tree.</summary>
internal sealed class FindingsFactViewer : ObservableObject, IFactViewer
{
    internal FindingsFactViewer(FactRow row, FactViewerContext context)
    {
        (Row, _) = (row, context);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The polite line that says the outcome of the read.</summary>
    public Components.LiveAnnouncer Voice => throw new NotSupportedException();

    /// <summary>True until the body has been read.</summary>
    public bool IsLoading => throw new NotSupportedException();

    /// <summary>The words while the body is read.</summary>
    public string LoadingText => throw new NotSupportedException();

    /// <summary>The served sentence when the body can't be shown; empty otherwise.</summary>
    public string ProblemText => throw new NotSupportedException();

    /// <summary>True when the body can't be shown.</summary>
    public bool HasProblem => throw new NotSupportedException();

    /// <summary>One row per finding.</summary>
    public IReadOnlyList<FindingRowViewModel> Findings => throw new NotSupportedException();

    /// <summary>True when the rows are shown.</summary>
    public bool ShowsFindings => throw new NotSupportedException();

    /// <summary>True when the body is an empty list.</summary>
    public bool ShowsNone => throw new NotSupportedException();

    /// <summary>"No findings".</summary>
    public string NoneText => throw new NotSupportedException();

    /// <summary>True when the body is not a findings list and the payload tree is shown.</summary>
    public bool ShowsTree => throw new NotSupportedException();

    /// <summary>The whole body as a tree (the fallback).</summary>
    public IReadOnlyList<PayloadNodeViewModel> Tree => throw new NotSupportedException();

    /// <summary>A body that is not an object or an array, as one line.</summary>
    public string ScalarLine => throw new NotSupportedException();

    /// <summary>True when there is a scalar line.</summary>
    public bool HasScalarLine => throw new NotSupportedException();

    /// <summary>The engine's truncation, in words; empty when the body is whole.</summary>
    public string TruncatedNote => throw new NotSupportedException();

    /// <summary>True when the body was cut short.</summary>
    public bool HasTruncatedNote => throw new NotSupportedException();

    /// <summary>Reads the fact's route.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the viewer shows the answer.</returns>
    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();
}
