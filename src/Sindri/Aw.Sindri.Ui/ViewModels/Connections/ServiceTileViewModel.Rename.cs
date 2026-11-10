using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The service tile (<see cref="ServiceTileViewModel"/>, continued): Rename on a connected tile's menu, an inline field holding the current label (#2556).</summary>
internal sealed partial class ServiceTileViewModel
{
    // The codes the app reads by name: no connection has the name, the new name is taken, the label is not allowed, the label is the one it has.
    private const string RenameUnknown = "SND-CONN-001";
    private const string RenameTaken = "SND-CONN-010";
    private const string RenameBad = "SND-CONN-008";
    private const string RenameSame = "SND-CLI-003";

    private bool _renameSending;

    /// <summary>True when the menu offers Rename: the tile has a connection.</summary>
    public bool CanRename => _row is not null;

    /// <summary>True while the inline name field is open.</summary>
    public bool IsRenaming { get; private set { Set(ref field, value); OnChanged(nameof(CanSaveRename)); } }

    /// <summary>The label typed in the field.</summary>
    public string RenameText { get; set { Set(ref field, value ?? string.Empty); OnChanged(nameof(CanSaveRename)); } } = string.Empty;

    /// <summary>Why the last rename was refused, in words (no code); empty otherwise.</summary>
    public string RenameError { get; private set { Set(ref field, value); OnChanged(nameof(HasRenameError)); } } = string.Empty;

    /// <summary>True when the last rename was refused.</summary>
    public bool HasRenameError => RenameError.Length > 0;

    /// <summary>True when Save does something: a label is typed and no rename is on the way.</summary>
    public bool CanSaveRename => IsRenaming && !_renameSending && RenameText.Trim().Length > 0;

    /// <summary>Opens the field with the current label.</summary>
    internal void StartRename()
    {
        if (_row is null)
        {
            return;
        }

        (RenameText, RenameError) = (Account, string.Empty);
        IsRenaming = true;
    }

    /// <summary>Closes the field and leaves the label as it was.</summary>
    internal void CancelRename()
    {
        IsRenaming = false;
        (RenameText, RenameError) = (Account, string.Empty);
    }

    /// <summary>Saves the typed label: sent once, nothing when it is the label already. A refusal keeps the field open and says why in words.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task Rename(CancellationToken cancellationToken)
    {
        var label = RenameText.Trim();
        if (!IsRenaming || _renameSending || label.Length == 0 || _row is null)
        {
            return;
        }

        if (string.Equals(label, Account, StringComparison.Ordinal))
        {
            CancelRename();
            return;
        }

        _renameSending = true;
        RenameError = string.Empty;
        OnChanged(nameof(CanSaveRename));
        try
        {
            var renamed = await _actions.Rename(ConnectionName, label, cancellationToken).ConfigureAwait(true);
            if (renamed.IsError)
            {
                RenameError = RenameRefused(renamed.Error, label);
                return;
            }

            IsRenaming = false;
            await _changed().ConfigureAwait(true);
        }
        finally
        {
            _renameSending = false;
            OnChanged(nameof(CanSaveRename));
        }
    }

    private string RenameRefused(Error error, string label) => error.Code switch
    {
        RenameTaken => Labels.RenameTaken(Service.Title, label),
        RenameBad => Labels.RenameBadLabel,
        RenameUnknown => Labels.RenameGone,
        RenameSame => Labels.RenameSame,
        _ => Labels.RenameFailed(error.Message),
    };
}
