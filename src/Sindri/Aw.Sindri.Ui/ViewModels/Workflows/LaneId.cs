namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The board's lanes, left to right (F3 workflows spec §2).</summary>
internal enum LaneId
{
    /// <summary>Scoping.</summary>
    Scoping,

    /// <summary>Design (redesign shares it).</summary>
    Design,

    /// <summary>Plan.</summary>
    Plan,

    /// <summary>Build and prove.</summary>
    Build,

    /// <summary>Merge: waits for the API to serve a line stage.</summary>
    Merge,

    /// <summary>Done: the finished runs.</summary>
    Done,
}
