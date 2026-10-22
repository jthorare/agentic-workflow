namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Which of the run canvas's edges one is.</summary>
internal enum RunEdgeKind
{
    /// <summary>Forward along the main path.</summary>
    Main,

    /// <summary>A loop: back to an earlier stage or out to a branch card.</summary>
    Loop,

    /// <summary>Into an end card.</summary>
    End,
}
