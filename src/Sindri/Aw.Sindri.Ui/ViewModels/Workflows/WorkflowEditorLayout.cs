namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The canvas layout the workflow.editor profile key picks (spec decision 14).</summary>
internal enum WorkflowEditorLayout
{
    /// <summary>The sheet-first layout: the canvas plus a right-hand sheet (the default).</summary>
    Sheet,

    /// <summary>The canvas-only layout of today.</summary>
    Steps,
}
