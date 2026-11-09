namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One step of the graph: where it sits, its words and, when a run is shown, its status.</summary>
internal sealed class GraphNodeViewModel : ObservableObject
{
    private bool _isSelected;

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

    /// <summary>The accessible name: title, type and status word.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether this is the selected node.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }
}
