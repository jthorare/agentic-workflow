using System.Collections.ObjectModel;
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
    private string? _refusedLabel;

    /// <summary>True when the menu offers Rename: the tile has a connection.</summary>
    public bool CanRename => _row is not null;

    /// <summary>True while the inline name field is open.</summary>
    public bool IsRenaming { get; private set { Set(ref field, value); OnChanged(nameof(CanSaveRename)); OnChanged(nameof(ShowSubtitle)); OnChanged(nameof(RenameReason)); OnChanged(nameof(HasRenameReason)); } }

    /// <summary>True when the tile shows its subtitle line (the account label when connected): not while the rename field takes its place.</summary>
    public bool ShowSubtitle => !IsRenaming;

    /// <summary>The label typed in the field.</summary>
    public string RenameText { get; set { if (_refusedLabel is not null && !string.Equals(value?.Trim(), _refusedLabel, StringComparison.Ordinal)) { _refusedLabel = null; RenameError = string.Empty; } Set(ref field, value ?? string.Empty); OnChanged(nameof(CanSaveRename)); OnChanged(nameof(RenameReason)); OnChanged(nameof(HasRenameReason)); } } = string.Empty;

    /// <summary>Why the last rename was refused, in words (no code); empty otherwise.</summary>
    public string RenameError { get; private set { Set(ref field, value); OnChanged(nameof(HasRenameError)); OnChanged(nameof(RenameReason)); OnChanged(nameof(HasRenameReason)); } } = string.Empty;

    /// <summary>True when the last rename was refused.</summary>
    public bool HasRenameError => RenameError.Length > 0;

    /// <summary>The line under the field: the refusal when there is one, else "Type a name." while the field is empty.</summary>
    public string RenameReason => !IsRenaming ? string.Empty : HasRenameError ? RenameError : RenameText.Trim().Length == 0 ? Labels.RenameEmpty : string.Empty;

    /// <summary>True when <see cref="RenameReason"/> has words.</summary>
    public bool HasRenameReason => RenameReason.Length > 0;

    /// <summary>True when Save does something: a label is typed, no rename is on the way, and it is not the text the server just refused.</summary>
    public bool CanSaveRename => IsRenaming && !_renameSending && RenameText.Trim().Length > 0 && !string.Equals(RenameText.Trim(), _refusedLabel, StringComparison.Ordinal);

    /// <summary>Every connection of this service, one line each (#2708).</summary>
    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = [];

    /// <summary>The connection the open field renames; null while it is closed.</summary>
    private ConnectionRowViewModel? _target;

    /// <summary>The connection name the open field renames ('linear:work'), or empty.</summary>
    public string RenamingName => _target?.Name ?? string.Empty;

    /// <summary>Opens the field on one connection's label: Save renames that connection, whichever row the tile reads first.</summary>
    /// <param name="row">The connection to rename.</param>
    internal void StartRename(ConnectionRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _target = row;
        _refusedLabel = null;
        (RenameText, RenameError) = (LabelOf(row.Name), string.Empty);
        IsRenaming = true;
        OnChanged(nameof(RenamingName));
    }

    private static string LabelOf(string name)
    {
        var at = name.IndexOf(':', StringComparison.Ordinal);
        return at >= 0 ? name[(at + 1)..] : name;
    }

    /// <summary>Opens the field with the current label.</summary>
    internal void StartRename()
    {
        if (_row is { } first)
        {
            StartRename(first);
        }
    }

    /// <summary>Closes the field and leaves the label as it was.</summary>
    internal void CancelRename()
    {
        IsRenaming = false;
        _refusedLabel = null;
        (RenameText, RenameError) = (_target is { } t ? LabelOf(t.Name) : Account, string.Empty);
        _target = null;
        OnChanged(nameof(RenamingName));
    }

    /// <summary>Saves the typed label: sent once, nothing when it is the label already. A refusal keeps the field open and says why in words.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task Rename(CancellationToken cancellationToken)
    {
        var label = RenameText.Trim();
        if (!IsRenaming || _renameSending || label.Length == 0 || _target is not { } row)
        {
            return;
        }

        if (string.Equals(label, LabelOf(row.Name), StringComparison.Ordinal))
        {
            CancelRename();
            return;
        }

        _renameSending = true;
        RenameError = string.Empty;
        OnChanged(nameof(CanSaveRename));
        try
        {
            await Send(row, label, cancellationToken).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // a dropped call or a failed re-read must show a sentence on the tile, not vanish in the discarded task
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            if (IsRenaming && ReferenceEquals(_target, row))
            {
                RenameError = Labels.RenameFailed;
            }
        }
        finally
        {
            _renameSending = false;
            OnChanged(nameof(CanSaveRename));
        }
    }

    // The call and its answer: a refusal keeps the field and its text; a success shows the name the server answered with now, the re-read only confirms it.
    private async Task Send(ConnectionRowViewModel row, string label, CancellationToken cancellationToken)
    {
        var renamed = await _actions.Rename(row.Name, label, cancellationToken).ConfigureAwait(true);
        if (!ReferenceEquals(_target, row))
        {
            if (!renamed.IsError)
            {
                await _changed().ConfigureAwait(true);   // the field moved to another connection meanwhile, but the rename happened: read again so the list shows the new name
            }

            return;   // this answer says nothing to the field on screen
        }

        if (renamed.IsError)
        {
            if (!IsRenaming)
            {
                return;   // the field closed meanwhile: nothing to say a refusal under
            }

            _refusedLabel = RefusesThisLabel(renamed.Error) ? label : null;   // an unknown failure says "Try again": Save stays on
            RenameError = RenameRefused(renamed.Error, label);
            return;
        }

        row.Update(row.View with { Name = renamed.Value.Name });
        Raise(nameof(ConnectionName), nameof(Account), nameof(Subtitle), nameof(AccessibleName));
        IsRenaming = false;
        _target = null;
        OnChanged(nameof(RenamingName));
        await _changed().ConfigureAwait(true);
    }

    private static bool RefusesThisLabel(Error error) => string.Equals(error.Code, RenameTaken, StringComparison.Ordinal) || string.Equals(error.Code, RenameBad, StringComparison.Ordinal) || string.Equals(error.Code, RenameSame, StringComparison.Ordinal);

    private string RenameRefused(Error error, string label) => error.Code switch
    {
        RenameTaken => Labels.RenameTaken(Service.Title, label),
        RenameBad => Labels.RenameBadLabel,
        RenameUnknown => Labels.RenameGone,
        RenameSame => Labels.RenameSame,
        _ => Labels.RenameFailed,
    };

    /// <summary>The one connection this tile shows, or none.</summary>
    /// <param name="row">The matching row.</param>
    internal void Bind(ConnectionRowViewModel? row) => Bind(row is null ? [] : [row]);

    /// <summary>The connections of this tile's service, in list order; the first one drives the chip and the primary button. The page sets them on every read.</summary>
    /// <param name="rows">The matching rows.</param>
    internal void Bind(IReadOnlyList<ConnectionRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var closeField = IsRenaming && (_target is null || !rows.Any(r => string.Equals(r.Name, _target.Name, StringComparison.Ordinal)));   // the field was for a connection that is gone
        _row = rows.Count > 0 ? rows[0] : null;
        Patch.To(Connections, rows);
        if (closeField)
        {
            CancelRename();
        }
        else if (IsRenaming && rows.FirstOrDefault(r => string.Equals(r.Name, _target?.Name, StringComparison.Ordinal)) is { } same)
        {
            _target = same;   // the read made a fresh row object for the same connection
        }

        Raise(nameof(IsOffered), nameof(IsConnected), nameof(NeedsAttention), nameof(ConnectionName), nameof(Account), nameof(LastUsedText), nameof(Subtitle), nameof(AccessibleName), nameof(HasMany));
        RaiseState();
    }

    /// <summary>True when the service has more than one connection: the tile then lists each with its own Rename.</summary>
    public bool HasMany => Connections.Count > 1;
}
