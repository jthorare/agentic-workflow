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
        Name = Labels.WfGraphNodeName(title, type, word.ToLowerInvariant());
    }

    /// <summary>The node's id.</summary>
    public string Id { get; }

    /// <summary>Card line 1: the title, or the block's name when the node has none (never the id).</summary>
    public string Title { get; }

    /// <summary>Card line 2: the step's one settings line (<see cref="GraphCardLine.Settings"/>); empty until the graph sets it.</summary>
    public string Line { get; internal init; } = string.Empty;

    /// <summary>The block type, such as "run_agent".</summary>
    public string Type { get; }

    /// <summary>Left edge in the graph's world.</summary>
    public double X { get; internal set; }

    /// <summary>Top edge in the graph's world.</summary>
    public double Y { get; internal set; }

    /// <summary>The card's width in the world; the wrap layout sets it to the text scale times the card width.</summary>
    public double Width { get; internal set; } = WorkflowGraphViewModel.CardWidth;

    /// <summary>The card's height in the world: 60, or 80 when its settings line wraps, times the text scale.</summary>
    public double Height { get; internal set; } = WorkflowGraphViewModel.CardHeight;

    /// <summary>The text scale the card was laid out at (1 until the wrap layout runs).</summary>
    internal double Scale { get; set; } = 1;

    /// <summary>The status glyph of the run shown, or empty.</summary>
    public string Glyph { get; }

    /// <summary>The status word of the run shown, or empty.</summary>
    public string StatusWord { get; }

    /// <summary>How the status is coloured.</summary>
    public GraphTone Tone { get; }

    /// <summary>The accessible name: title, type and status word.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether this is the selected node.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }
}
