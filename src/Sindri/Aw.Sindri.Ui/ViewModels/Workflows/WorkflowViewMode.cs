namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>How the Workflows page draws its runs (F3 workflows spec §3).</summary>
internal enum WorkflowViewMode
{
    /// <summary>Stage lanes with a card per run.</summary>
    Board,

    /// <summary>The grouped list.</summary>
    List,
}
