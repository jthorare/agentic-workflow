namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The service tile (<see cref="ServiceTileViewModel"/>, continued): Rename on a connected tile's menu, an inline field holding the current label (#2556).</summary>
internal sealed partial class ServiceTileViewModel
{
    /// <summary>True when the menu offers Rename: the tile has a connection.</summary>
    public bool CanRename => _row is null;

    /// <summary>True while the inline name field is open.</summary>
    public bool IsRenaming { get; private set => Set(ref field, value); }

    /// <summary>The label typed in the field.</summary>
    public string RenameText { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>Why the last rename was refused, in words; empty otherwise.</summary>
    public string RenameError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the last rename was refused.</summary>
    public bool HasRenameError => RenameError.Length > 0;

    /// <summary>True when Save does something: a label is typed and no rename is on the way.</summary>
    public bool CanSaveRename => IsRenaming && RenameText.Trim().Length > 0;

    /// <summary>Opens the field with the current label.</summary>
    internal void StartRename() => throw new NotSupportedException();

    /// <summary>Closes the field and leaves the label as it was.</summary>
    internal void CancelRename() => throw new NotSupportedException();

    /// <summary>Saves the typed label: sent once, nothing when it is the label already.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal Task Rename(CancellationToken cancellationToken) => throw new NotSupportedException();
}
