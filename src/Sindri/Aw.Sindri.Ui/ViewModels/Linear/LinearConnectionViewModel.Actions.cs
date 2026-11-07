using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Settings' Linear card (<see cref="LinearConnectionViewModel"/>, continued): the buttons, each one call to the API.</summary>
internal sealed partial class LinearConnectionViewModel
{
    /// <summary>Reads the teams the pasted read key can see.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task ListTeams(CancellationToken cancellationToken)
    {
        if (!CanListTeams || _url() is not { } url)
        {
            return;
        }

        (_error, _lookingUp) = (string.Empty, true);
        ForgetAnswer();
        Changed();
        Result<LinearTeamChoices> read;
        try
        {
            read = await _api.LinearConnectionTeams(url, _readKey, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _lookingUp = false;
        }

        if (read.IsError)
        {
            (Teams, _error) = ([], ErrorOf(read.Error, write: false));
        }
        else
        {
            var stored = TeamKeys.ToHashSet(StringComparer.Ordinal);   // a replaced key keeps the teams already chosen
            Teams = [.. read.Value.Teams.DistinctBy(t => t.Key, StringComparer.Ordinal).Select(t => new LinearTeamTickViewModel(t, Changed) { IsTicked = stored.Contains(t.Key) })];
        }

        Changed();
    }

    /// <summary>Stores the read key with the ticked teams.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task SaveRead(CancellationToken cancellationToken)
    {
        if (!CanSaveRead || _url() is not { } url)
        {
            return;
        }

        var (key, ticked) = (_readKey, (IReadOnlyList<string>)[.. Teams.Where(t => t.IsTicked).Select(t => t.Key)]);
        var sent = await Send(url, "read", key, ticked, cancellationToken).ConfigureAwait(true);
        if (sent is not null)
        {
            (Teams, _replacingRead) = ([], false);
            await Settled(url, sent.Approved ? string.Empty : Labels.LinearConnWaitingRead, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Stores the write key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task CheckWrite(CancellationToken cancellationToken)
    {
        if (!CanCheckWrite || _url() is not { } url)
        {
            return;
        }

        var sent = await Send(url, "write", _writeKey, teams: null, cancellationToken).ConfigureAwait(true);
        if (sent is not null)
        {
            _replacingWrite = false;
            await Settled(url, sent.Approved ? string.Empty : Labels.LinearConnWaiting, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Replace key on the read line: the paste box comes back.</summary>
    internal void ReplaceRead()
    {
        (_replacingRead, _error) = (true, string.Empty);
        Changed();
    }

    /// <summary>Replace key on the write line: the paste box comes back.</summary>
    internal void ReplaceWrite()
    {
        (_replacingWrite, _error) = (true, string.Empty);
        Changed();
    }

    /// <summary>Cancel (or Esc) on the read line's paste box: back to the read-set line, nothing changed.</summary>
    internal void CancelReplaceRead()
    {
        (_replacingRead, _readKey, Teams, _error) = (false, string.Empty, [], string.Empty);
        Changed();
    }

    /// <summary>Cancel (or Esc) on the write line's paste box: back to the write-set line, nothing changed.</summary>
    internal void CancelReplaceWrite()
    {
        (_replacingWrite, _writeKey, _error) = (false, string.Empty, string.Empty);
        Changed();
    }

    /// <summary>Remove key: asks first.</summary>
    internal void AskRemove()
    {
        IsConfirmingRemove = true;
        Changed();
    }

    /// <summary>Keep it: the question goes away.</summary>
    internal void KeepWrite()
    {
        IsConfirmingRemove = false;
        Changed();
    }

    /// <summary>Remove, after the question: deletes the write key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RemoveWrite(CancellationToken cancellationToken)
    {
        if (IsBusy || _url() is not { } url)
        {
            return;
        }

        (IsConfirmingRemove, _removing, _saving, _error) = (false, true, true, string.Empty);
        ForgetAnswer();
        Changed();
        Result<LinearDisconnected> removed;
        try
        {
            removed = await _api.LinearDisconnectWrite(url, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            (_removing, _saving) = (false, false);
        }

        if (removed.IsError)
        {
            _error = string.Equals(removed.Error.Code, "SND-LINEAR-014", StringComparison.Ordinal) ? Labels.LinearConnRemoveKeychainFailed : Labels.LinearConnFailed(removed.Error);
            Changed();
            return;
        }

        _replacingWrite = false;
        await Settled(url, removed.Value.Approved ? string.Empty : Labels.LinearConnWaiting, cancellationToken).ConfigureAwait(true);
    }

    // One save of a key: the key is dropped from the box as it goes, and kept nowhere. Null when Sindri refused it (the refusal is shown, nothing was stored).
    private async Task<LinearConnected?> Send(Uri url, string kind, string key, IReadOnlyList<string>? teams, CancellationToken cancellationToken)
    {
        (_saving, _checkingWrite, _error) = (true, kind == "write", string.Empty);
        ForgetAnswer();
        Changed();
        Result<LinearConnected> saved;
        try
        {
            saved = await _api.LinearConnect(url, kind, key, teams, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            (_saving, _checkingWrite) = (false, false);
            (_readKey, _writeKey) = kind == "write" ? (_readKey, string.Empty) : (string.Empty, _writeKey);
        }

        if (saved.IsError)
        {
            _error = ErrorOf(saved.Error, write: kind == "write");
            Changed();
            return null;
        }

        return saved.Value;
    }

    // A change Sindri took: said when it waits for the approval, and the connection is read again.
    private Task Settled(Uri url, string waiting, CancellationToken cancellationToken)
    {
        (_waiting, _fresh) = (waiting, true);
        Changed();
        return Read(url, settled: true, cancellationToken);
    }

    // The click's own answer is dropped by the next button press, not by a refresh.
    private void ForgetAnswer() => (_waiting, _fresh) = (string.Empty, false);

    /// <summary>The Settings page was left: the click's own answer ("Saved.") is dropped, so the next open reads what is true then.</summary>
    internal void Leave()
    {
        ForgetAnswer();
        Changed();
    }

    private static string ErrorOf(Error error, bool write) => error.Code switch
    {
        "SND-LINEAR-004" => write ? Labels.LinearConnWriteRefused : Labels.LinearConnReadRefused,
        "SND-LINEAR-014" => Labels.LinearConnNoKeychain,
        "SND-LINEAR-010" => Labels.LinearConnUnreachable,
        "SND-LINEAR-001" => Labels.LinearConnSameAsRead,
        "SND-LINEAR-003" => Labels.LinearConnNoTeams,
        _ => Labels.LinearConnFailed(error),
    };
}
