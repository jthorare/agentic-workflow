namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The graph edit commands (<see cref="GraphEditViewModel"/>, continued): their words.</summary>
internal sealed partial class GraphEditViewModel
{
    /// <summary>True while the editor sheet is open on this page; set by the page.</summary>
    internal bool SheetOpen { get; set; }

    /// <summary>The line explaining why graph edits are off while the editor sheet is open; empty otherwise.</summary>
    public string SheetOpenText => SheetOpen && Editor is null ? Labels.WfEditSheetOpenNote : string.Empty;   // red stub: the green commit shows it while the sheet is open

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The Add step command's words.</summary>
    public string AddText => Labels.WfEditAdd;

    /// <summary>The Remove command's words.</summary>
    public string RemoveText => Labels.WfEditRemove;

    /// <summary>The Connect to command's words.</summary>
    public string ConnectText => Labels.WfEditConnect;

    /// <summary>The remove confirm's yes.</summary>
    public string ConfirmYesText => Labels.WfEditConfirmYes;

    /// <summary>The close action of the picker, the target list and the confirm.</summary>
    public string CancelText => Labels.WfEditCancel;

    /// <summary>The block picker's heading.</summary>
    public string PickBlockText => Labels.WfEditPickBlock;

    /// <summary>The target list's heading.</summary>
    public string PickTargetText => Labels.WfEditPickTarget;

    /// <summary>The accessible name of the edit commands.</summary>
    public string BarName => Labels.WfEditBarName;

    /// <summary>The Reload command's words.</summary>
    public string ReloadText => Labels.WfReload;
#pragma warning restore CA1822, MA0041
}
