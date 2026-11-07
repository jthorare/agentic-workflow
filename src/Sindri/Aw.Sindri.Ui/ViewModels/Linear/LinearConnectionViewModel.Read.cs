namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Settings' Linear card (<see cref="LinearConnectionViewModel"/>, continued): reading the connection, and asking for the card to be revealed once it is read.</summary>
internal sealed partial class LinearConnectionViewModel
{
    /// <summary>Reads the connection. What an earlier save said about waiting changes is dropped: this read says what is true now.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the card is up to date.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => Read(url, keepWaiting: false, cancellationToken);

    /// <summary>Asks for the card to be shown and focused; done as soon as the next read is in (at once when the API is not known).</summary>
    internal void Reveal()
    {
        _revealPending = true;
        if (_url() is null)
        {
            RaiseReveal();
        }
    }

    /// <summary>The view attached: raises a reveal that was asked for while no API was known and no view was there to hear it.</summary>
    internal void RevealIfWaiting() => _generation += 0;   // red stub

    /// <summary>Review changes was pressed.</summary>
    internal void RequestReview() => ReviewRequested?.Invoke(this, EventArgs.Empty);

    private async Task Read(Uri url, bool keepWaiting, CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        var read = await _api.LinearConnection(url, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read started meanwhile
        }

        if (read.IsError)
        {
            var session = string.Equals(read.Error.Code, SessionRefusal, StringComparison.Ordinal);
            (IsAvailable, _readError) = (!session, session || _connection is not null ? string.Empty : Labels.LinearConnReadFailed(read.Error));
        }
        else
        {
            (_connection, IsAvailable, _readError) = (read.Value, true, string.Empty);
            _waiting = keepWaiting ? _waiting : string.Empty;
        }

        Changed();
        if (IsAvailable)
        {
            RaiseRevealIfAsked();
        }
        else
        {
            _revealPending = false;
        }
    }

    private void RaiseRevealIfAsked()
    {
        if (_revealPending)
        {
            RaiseReveal();
        }
    }

    private void RaiseReveal()
    {
        _revealPending = false;
        RevealRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Changed()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
