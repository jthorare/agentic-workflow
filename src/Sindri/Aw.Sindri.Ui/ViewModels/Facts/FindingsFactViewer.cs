using System.Text.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// A findings fact (<c language="text">fact:findings</c>, a JSON array of <c language="text">{ rule, message, path?, line?, detail? }</c>): View reads the fact's route and shows a row per finding. An empty list says "No findings".
/// A body that is not a list of findings is drawn as the payload tree, so a fact is never lost.
/// </summary>
internal sealed class FindingsFactViewer : ObservableObject, IFactViewer
{
    private readonly FactViewerContext _context;
    private Shape _shape = Shape.Tree;

    internal FindingsFactViewer(FactRow row, FactViewerContext context)
    {
        (Row, _context) = (row, context);
    }

    private enum Shape
    {
        Tree,
        Rows,
        None,
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The polite line that says the outcome of the read.</summary>
    public Components.LiveAnnouncer Voice { get; } = new(Labels.ReadingFact);

    /// <summary>True until the body has been read.</summary>
    public bool IsLoading { get; private set { Set(ref field, value); Refresh(); } } = true;

    /// <summary>The words while the body is read.</summary>
#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    public string LoadingText => Labels.ReadingFact;

    /// <summary>"No findings".</summary>
    public string NoneText => Labels.FindingsNone;
#pragma warning restore CA1822, MA0041

    /// <summary>The served sentence when the body can't be shown; empty otherwise.</summary>
    public string ProblemText { get; private set { Set(ref field, value); OnChanged(nameof(HasProblem)); Refresh(); } } = string.Empty;

    /// <summary>True when the body can't be shown.</summary>
    public bool HasProblem => ProblemText.Length > 0;

    /// <summary>One row per finding.</summary>
    public IReadOnlyList<FindingRowViewModel> Findings { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the rows are shown.</summary>
    public bool ShowsFindings => Ready && _shape == Shape.Rows;

    /// <summary>True when the body is an empty list.</summary>
    public bool ShowsNone => Ready && _shape == Shape.None;

    /// <summary>True when the body is not a findings list and the payload tree is shown.</summary>
    public bool ShowsTree => Ready && _shape == Shape.Tree;

    /// <summary>The whole body as a tree (the fallback).</summary>
    public IReadOnlyList<PayloadNodeViewModel> Tree { get; private set => Set(ref field, value); } = [];

    /// <summary>A body that is not an object or an array, as one line; empty when the tree draws the body.</summary>
    public string ScalarLine { get; private set { Set(ref field, value); OnChanged(nameof(HasScalarLine)); } } = string.Empty;

    /// <summary>True when the body is a lone value the tree can't draw.</summary>
    public bool HasScalarLine => ScalarLine.Length > 0;

    /// <summary>The engine's truncation, in words; empty when the body is whole.</summary>
    public string TruncatedNote { get; private set { Set(ref field, value); OnChanged(nameof(HasTruncatedNote)); } } = string.Empty;

    /// <summary>True when the body was cut short.</summary>
    public bool HasTruncatedNote => TruncatedNote.Length > 0;

    private bool Ready => !IsLoading && !HasProblem;

    /// <summary>Reads the fact's route.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the viewer shows the answer.</returns>
    public async Task Load(CancellationToken cancellationToken)
    {
        if (_context.ApiUrl() is not { } url)
        {
            ProblemText = Labels.PayloadNotRunning;
            await Task.Yield();   // the live region is created with the viewer: its first words are said a turn later, so the change is spoken
            Voice.Say(ProblemText);
            IsLoading = false;
            return;
        }

        var answer = await _context.Api.InstanceFact(url, _context.InstanceId, Row.Key, cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            ProblemText = string.Equals(answer.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? Labels.StripUnreadable : answer.Error.Message;
            Voice.Say(ProblemText);
        }
        else
        {
            Show(answer.Value);
            Voice.Say(TruncatedNote.Length > 0 ? TruncatedNote : Labels.PayloadLoaded(Row.Label));
        }

        IsLoading = false;
    }

    private void Show(FactView fact)
    {
        TruncatedNote = fact.Truncated ? Labels.PayloadTruncated : string.Empty;
        if (Rows(fact.Body) is { } rows)
        {
            Findings = rows;
            _shape = rows.Count == 0 ? Shape.None : Shape.Rows;
        }
        else
        {
            Tree = PayloadNodeViewModel.Roots(fact.Body);
            ScalarLine = fact.Body.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? string.Empty : PayloadNodeViewModel.Line(fact.Body);
            _shape = Shape.Tree;
        }

        Refresh();
    }

    private void Refresh()
    {
        OnChanged(nameof(ShowsFindings));
        OnChanged(nameof(ShowsNone));
        OnChanged(nameof(ShowsTree));
    }

    // The rows of a list whose every entry is an object with a string 'rule' and a string 'message'; null for anything else.
    private static List<FindingRowViewModel>? Rows(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var rows = new List<FindingRowViewModel>();
        foreach (var entry in body.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || Text(entry, "rule") is not { } rule || Text(entry, "message") is not { } message)
            {
                return null;
            }

            rows.Add(new FindingRowViewModel(rule, message, Text(entry, "path"), Line(entry), Text(entry, "detail")));
        }

        return rows;
    }

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Line(JsonElement entry) =>
        entry.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var line) ? line : null;
}
