namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What a key does on the workflow editor (spec 4.6).</summary>
internal enum EditorKeyAction
{
    /// <summary>The key is not the editor's.</summary>
    None,

    /// <summary>An arrow: walk the graph.</summary>
    WalkGraph,

    /// <summary>Open Add step.</summary>
    AddStep,

    /// <summary>Open the selected step's sheet.</summary>
    OpenSheet,

    /// <summary>Close the topmost layer.</summary>
    Close,

    /// <summary>Rename the step.</summary>
    Rename,

    /// <summary>Open Test run.</summary>
    TestRun,

    /// <summary>Fit the graph.</summary>
    FitGraph,

    /// <summary>Save the step in the sheet.</summary>
    SaveStep,

    /// <summary>Undo the last save, while its toast is up.</summary>
    Undo,

    /// <summary>Activate in one click.</summary>
    ActivateNow,
}
