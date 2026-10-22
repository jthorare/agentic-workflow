namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Which overlay covers the stage (F3 spec 1 §3).</summary>
internal enum ContextOverlay
{
    /// <summary>None: the map, the files or the refusal banner shows.</summary>
    None,

    /// <summary>A map build is running: skeleton lanes and a message.</summary>
    Building,

    /// <summary>The map does not exist: a message and a button that builds it.</summary>
    Empty,
}
