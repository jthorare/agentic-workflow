namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>How the run canvas draws one edge (F3 spec 3 §2, "Edge colour rule"). The hover tone is the control's, not the model's.</summary>
internal enum EdgeTone
{
    /// <summary>Wire, 2 px solid.</summary>
    Normal,

    /// <summary>PrimaryHover, 2 px solid: the stage before is done and the one it leads to is where the run is.</summary>
    Active,

    /// <summary>Error, 2 px solid: an end edge whose source stage failed.</summary>
    Failed,

    /// <summary>Wire, dashed 6 5, at 70 %: not taken, not reachable yet, a loop, or leaving a stage that ended badly.</summary>
    Dim,
}
