namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One step of the graph: where it sits, its words and, when a run is shown, its status.</summary>
internal sealed class GraphNodeViewModel : ObservableObject
{
    internal GraphNodeViewModel(string id, string title, string type, double x, double y, string? status)
    {
        Id = id;
        Title = title;
        Type = type;
        X = x;
        Y = y;
        var (glyph, word) = status is null ? (string.Empty, string.Empty) : Labels.WfChip(status);
        Glyph = glyph;
        StatusWord = word;
        Tone = status switch
        {
            null => GraphTone.None,
            "succeeded" => GraphTone.Success,
            "failed" => GraphTone.Error,
            "running" or "waiting" or "queued" => GraphTone.Attention,
            "skipped" or "cancelled" => GraphTone.Muted,
            _ => GraphTone.None,
        };
        StatusLower = word.ToLowerInvariant();
    }

    /// <summary>The node's id.</summary>
    public string Id { get; }

    /// <summary>The title, or the id when the node has none.</summary>
    public string Title { get; }

    /// <summary>The block type, such as "run_agent".</summary>
    public string Type { get; }

    /// <summary>Left edge in the graph's world.</summary>
    public double X { get; }

    /// <summary>Top edge in the graph's world.</summary>
    public double Y { get; }

    /// <summary>The status glyph of the run shown, or empty.</summary>
    public string Glyph { get; }

    /// <summary>The status word of the run shown, or empty.</summary>
    public string StatusWord { get; }

    /// <summary>How the status is coloured.</summary>
    public GraphTone Tone { get; }

    /// <summary>The kind's one name, as the Add step menu says it ("If" for "condition").</summary>
    public string Kind => StepKinds.Name(Type);

    /// <summary>The accessible name: "title, kind step, n of m", then the status word when a run is shown ("Check, Prompt step, 2 of 4, failed").</summary>
    public string Name => Labels.WfGraphNodeName(Title, Kind, Position, Count, StatusLower);

    /// <summary>The accessible name with a given place and no status word: "Check, Prompt step, 2 of 4".</summary>
    /// <param name="position">The 1-based place in reading order.</param>
    /// <param name="count">How many steps the graph has.</param>
    /// <returns>The name.</returns>
    public string PositionName(int position, int count) => Labels.WfGraphNodeName(Title, Kind, position, count, word: string.Empty);

    /// <summary>Sets the step's place in the graph's reading order, for its accessible name.</summary>
    /// <param name="position">The 1-based place.</param>
    /// <param name="count">How many steps the graph draws.</param>
    internal void Place(int position, int count) => (Position, Count) = (position, count);

    private string StatusLower { get; }

    private int Position { get; set; } = 1;

    private int Count { get; set; } = 1;

    /// <summary>Gets a value indicating whether this is the selected node.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }
}
