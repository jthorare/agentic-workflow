using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map inside a concept (<see cref="WholeMapViewModel"/>, continued): the breadcrumb runs from the concepts through the concept, and a back button goes up a layer.</summary>
internal sealed partial class WholeMapViewModel
{
    private const string ConceptsPath = "concepts";
    private const string ConceptPath = "concept";
    private const string FeaturePath = "feature";

    private string? _conceptName;

    /// <summary>Raised when the "Concepts" step of the breadcrumb is pressed: the page goes back to the concept layer.</summary>
    internal event EventHandler? ConceptsRequested;

    /// <summary>Raised when the back button is pressed: the page goes up one layer.</summary>
    internal event EventHandler? UpRequested;

    /// <summary>Whether the map is scoped to a concept.</summary>
    public bool HasConcept => _conceptName is not null;

    /// <summary>The back button's words.</summary>
    public string BackText { get; } = Labels.ContextConceptsBack;

    /// <summary>The map is scoped to a concept: what was drawn goes, and the breadcrumb starts at the concepts.</summary>
    /// <param name="name">The concept's name.</param>
    internal void EnterConcept(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Clear();
        _conceptName = name;
        Changed();
        OnChanged(nameof(HasConcept));
    }

    /// <summary>The map is no longer scoped: what was drawn goes.</summary>
    internal void LeaveConcept()
    {
        Clear();
        _conceptName = null;
        Changed();
        OnChanged(nameof(HasConcept));
    }

    /// <summary>The back button was pressed.</summary>
    internal void GoUp()
    {
        if (!ExitDrill())   // a drilled band goes first: back to the concept's whole map
        {
            UpRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A step of the breadcrumb was pressed.</summary>
    /// <param name="crumb">The step.</param>
    internal void Navigate(Crumb crumb)
    {
        ArgumentNullException.ThrowIfNull(crumb);
        if (string.Equals(crumb.Path, ConceptsPath, StringComparison.Ordinal))
        {
            ConceptsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsDrilled && string.Equals(crumb.Path, ConceptPath, StringComparison.Ordinal))
        {
            _ = Panel.Close();
            _ = ExitDrill();
            return;
        }

        if (string.Equals(crumb.Path, FeaturePath, StringComparison.Ordinal) || string.Equals(crumb.Path, BandPath, StringComparison.Ordinal))
        {
            _ = Panel.Close();   // the feature is where you are once the node's panel is shut
            return;
        }

        _ = Panel.Close();
        GoHome();
    }

    // The breadcrumb: "~" on the whole map; inside a concept, "Concepts / Self scheduling" in its place. Each step but the last is a link.
    private IReadOnlyList<Crumb> CrumbList()
    {
        var node = HasConcept && Panel.IsOpen && Panel.Title.Length > 0;
        var deeper = IsZoomed || IsAreaOpen || IsDrilled || node;
        var below = Below(node);
        IReadOnlyList<Crumb> head = HasConcept
            ? [new Crumb(Labels.ContextConceptsCrumb, ConceptsPath, IsLink: true), new Crumb(_conceptName ?? string.Empty, ConceptPath, deeper)]
            : [Crumb.Top(deeper)];
        IReadOnlyList<Crumb> last = node ? [new Crumb(Panel.Title, Path: null, IsLink: false)] : [];
        return [.. head, .. below, .. last];
    }

    // The step under the concept: the query's answer, the open feature or the drilled band.
    private IReadOnlyList<Crumb> Below(bool node) =>
        IsZoomed ? [new Crumb(Labels.ContextCrumbAnswer, FeaturePath, node)]
        : IsAreaOpen ? [new Crumb(AreaTitle, FeaturePath, node)]
        : _drilled is { } band ? [new Crumb(NodeBands.Title(band), BandPath, node)]
        : [];

    // Forgets the map drawn: nothing is read and nothing is pending.
    private void Clear()
    {
        (_overview, _beforeZoom, IsAreaOpen, AreaTitle) = (null, null, false, string.Empty);
        (HitKeys, IsZoomed, MoreTouchedText, _zoomSet) = (new HashSet<string>(StringComparer.Ordinal), false, string.Empty, null);
        (SearchActive, NoMatches, _searchSet) = (false, false, null);
        (_drilled, _drillOwn, _drillCut) = (null, false, null);
        _ = Panel.Close();
        (Answer, Geometry) = (null, null);
        Areas = [];
        ForgetHighlight();
        (Head, SizeText, ByAreaNote) = (string.Empty, string.Empty, string.Empty);
        (IsReady, IsLoading, IsFailed) = (false, false, false);
    }
}
