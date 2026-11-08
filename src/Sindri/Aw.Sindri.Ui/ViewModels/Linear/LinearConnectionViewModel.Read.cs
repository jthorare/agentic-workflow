namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Settings' Linear card (<see cref="LinearConnectionViewModel"/>, continued): reading the connection, and asking for the card to be revealed once it is read.</summary>
internal sealed partial class LinearConnectionViewModel
{
    /// <summary>Reads the connection. A click's own answer (waiting changes, "Saved.") stays through this read unless the read says nothing is pending any more; the next button press drops it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the card is up to date.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => Read(url, settled: false, cancellationToken);

    /// <summary>Asks for the card to be shown and focused; done as soon as the next read is in (at once when the API is not known).</summary>
    internal void Reveal()
    {
        _revealPending = true;
        if (_url() is null)
        {
            RaiseReveal();   // heard only when the view is already attached; else RevealIfWaiting raises it then
        }
    }

    /// <summary>The view attached: raises a reveal that was asked for before it was there to hear it, when nothing is left to wait for (no API known, or the connection already read).</summary>
    internal void RevealIfWaiting()
    {
        if (_url() is null || _connection is not null)
        {
            RaiseRevealIfAsked();
        }
    }

    /// <summary>Review changes was pressed.</summary>
    internal void RequestReview() => ReviewRequested?.Invoke(this, EventArgs.Empty);

    private async Task Read(Uri url, bool settled, CancellationToken cancellationToken)
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
            if (!settled && !read.Value.Pending)
            {
                _waiting = string.Empty;   // nothing waits any more (approved elsewhere): the click's line would lie
            }
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
        if (RevealRequested is { } handler)
        {
            _revealPending = false;
            handler(this, EventArgs.Empty);
        }
    }

    private void Changed()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
