using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The service tile (<see cref="ServiceTileViewModel"/>, continued): "Replace token" on a connected rest or cli tile (#4358). It opens a paste box; Replace sends the new key once (PUT /sindri/connections/{name}/key).
/// Sindri checks the new key like a new one and keeps the old key until it passes, so a refusal leaves the tile connected and says so under the box.
/// </summary>
internal sealed partial class ServiceTileViewModel
{
    private bool _replaceBusy;

    /// <summary>The new token, pasted into the box.</summary>
    public SecretField ReplaceToken { get; } = new();

    /// <summary>True when the tile can take a new token: it is connected and holds a key (rest or cli), not a browser sign-in.</summary>
    public bool CanReplace => _row is { IsConnected: true } row && (string.Equals(row.View.Kind, "rest", StringComparison.Ordinal) || string.Equals(row.View.Kind, "cli", StringComparison.Ordinal));

    /// <summary>True while the paste box is open.</summary>
    public bool IsReplacing { get; private set { Set(ref field, value); RaiseReplace(); } }

    /// <summary>True when the "Replace token" link shows.</summary>
    public bool ShowReplaceLink => CanReplace && !IsReplacing;

    /// <summary>True while the new token is being checked.</summary>
    public bool IsReplaceChecking { get; private set { Set(ref field, value); RaiseReplace(); } }

    /// <summary>The line shown while the new token is checked; empty otherwise.</summary>
    public string ReplaceStatus => IsReplaceChecking ? Labels.TileReplaceChecking : string.Empty;

    /// <summary>True when the check line shows.</summary>
    public bool HasReplaceStatus => IsReplaceChecking;

    /// <summary>Why the last replacement did not happen, in words; empty otherwise.</summary>
    public string ReplaceError { get; private set { Set(ref field, value); ReplaceErrorIsCaution = false; Raise(nameof(HasReplaceError)); } } = string.Empty;

    /// <summary>True when the last replacement was refused or failed.</summary>
    public bool HasReplaceError => ReplaceError.Length > 0;

    /// <summary>True when the check never judged the token (not reached, rate limited): amber, not red.</summary>
    public bool ReplaceErrorIsCaution { get; private set => Set(ref field, value); }

    /// <summary>"Token replaced, not checked" after a replacement the service did not check (a cli connection, or a preset with no key check); empty otherwise.</summary>
    public string ReplaceNote { get; private set { Set(ref field, value); Raise(nameof(HasReplaceNote)); } } = string.Empty;

    /// <summary>True when the note shows: only on a tile that can be replaced again, and not while the box is open.</summary>
    public bool HasReplaceNote => ReplaceNote.Length > 0 && ShowReplaceLink;

    /// <summary>True when Replace does something: a token is pasted and no check runs.</summary>
    public bool CanSendReplace => IsReplacing && !IsReplaceChecking && ReplaceToken.HasValue;

    /// <summary>Opens the paste box.</summary>
    internal void StartReplace()
    {
        if (CanReplace)
        {
            _ = ReplaceToken.Take();
            (ReplaceError, ReplaceNote) = (string.Empty, string.Empty);
            IsReplacing = true;
        }
    }

    /// <summary>Closes the paste box and forgets what was pasted.</summary>
    internal void CancelReplace()
    {
        if (_replaceBusy)
        {
            return;
        }

        _ = ReplaceToken.Take();
        ReplaceError = string.Empty;
        IsReplacing = false;
    }

    /// <summary>Sends the pasted token once. A refusal keeps the box open and the tile connected; a success closes the box and reads the tile again.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task Replace(CancellationToken cancellationToken)
    {
        if (!CanSendReplace || _row is not { } row)
        {
            return;
        }

        (_replaceBusy, ReplaceError, IsReplaceChecking) = (true, string.Empty, true);
        try
        {
            var replaced = await _actions.ReplaceKey(row.Name, new ReplaceConnectionKeyBody(ReplaceToken.Text), cancellationToken).ConfigureAwait(true);
            await Land(replaced).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // a dropped call must show a sentence on the tile, not vanish in the discarded task
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            ReplaceError = Labels.ReplaceTokenFailed(new Error("SND-UI-002", ErrorKind.Unavailable, "no answer"), Service.Title);
        }
        finally
        {
            (_replaceBusy, IsReplaceChecking) = (false, false);
            Raise(nameof(CanSendReplace));
        }
    }

    // The answer: a refusal keeps the box and the token (only an unjudged one stays in it for another try); a success forgets the token and closes the box.
    private async Task Land(Result<ConnectionKeyReplaced> replaced)
    {
        if (replaced.IsError)
        {
            var unjudged = string.Equals(replaced.Error.Code, "SND-CONN-019", StringComparison.Ordinal) || string.Equals(replaced.Error.Code, "SND-CONN-020", StringComparison.Ordinal);
            if (!unjudged)
            {
                _ = ReplaceToken.Take();
            }

            ReplaceError = Labels.ReplaceTokenFailed(replaced.Error, Service.Title);
            ReplaceErrorIsCaution = unjudged;
            return;
        }

        _ = ReplaceToken.Take();
        ReplaceNote = replaced.Value.Checked ? string.Empty : Labels.TileReplacedUnchecked(replaced.Value.UncheckedReason);
        IsReplacing = false;
        await _changed().ConfigureAwait(true);
    }

    private void RaiseReplace() => Raise(nameof(CanReplace), nameof(ShowReplaceLink), nameof(HasReplaceNote), nameof(CanSendReplace), nameof(ReplaceStatus), nameof(HasReplaceStatus));
}
