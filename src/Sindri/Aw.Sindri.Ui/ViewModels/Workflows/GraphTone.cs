namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>How a graph step's status is coloured; always drawn with a glyph too, never colour alone.</summary>
internal enum GraphTone
{
    /// <summary>No run is shown, or the status is unknown.</summary>
    None,

    /// <summary>Succeeded.</summary>
    Success,

    /// <summary>Failed.</summary>
    Error,

    /// <summary>Running, waiting or queued.</summary>
    Attention,

    /// <summary>Skipped or cancelled.</summary>
    Muted,
}
