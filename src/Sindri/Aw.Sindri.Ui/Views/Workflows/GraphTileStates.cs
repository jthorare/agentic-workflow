namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The states a graph step tile (or a branch pill) can be in at once; the canvas combines them (a selected tile can also hold focus and be hovered).</summary>
[Flags]
internal enum GraphTileStates
{
    /// <summary>Nothing special.</summary>
    None = 0,

    /// <summary>The pointer is over it.</summary>
    Hover = 1,

    /// <summary>It holds keyboard focus (a ring, drawn outside the selected ring).</summary>
    Focus = 2,

    /// <summary>It is the selected one (the inner ring).</summary>
    Selected = 4,

    /// <summary>It cannot be used now (dashed, tertiary ink, no hover).</summary>
    Disabled = 8,

    /// <summary>Its step failed or is invalid (the error fill and border).</summary>
    Error = 16,
}
