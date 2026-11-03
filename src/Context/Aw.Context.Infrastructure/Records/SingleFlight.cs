namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// One run of a read at a time: callers that arrive while it runs wait on the same run (#1951). The run stops only when
/// every caller has gone; the next call after that, or after it ends, starts a new one.
/// </summary>
/// <typeparam name="T">What the read returns.</typeparam>
internal sealed class SingleFlight<T>
{
    private readonly Lock _gate = new();
    private Flight? _current;

    /// <summary>Runs <paramref name="work"/>, or joins the run already in flight.</summary>
    /// <param name="work">The read; its token is cancelled when every caller has gone.</param>
    /// <param name="cancellationToken">Stops this caller's wait.</param>
    /// <returns>The run's value.</returns>
    public async ValueTask<T> Run(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        Flight flight;
        lock (_gate)
        {
            if (_current is null)
            {
                _current = new Flight();
                var started = _current;
                // why: on its own thread, so the run belongs to no one caller and a caller leaving never ends it alone
                started.Task = Task.Run(() => work(started.Stop.Token), CancellationToken.None);
                _ = started.Task.ContinueWith(_ => Land(started), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            flight = _current;
            flight.Waiters++;
        }

        try
        {
            return await flight.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Leave(flight);
        }
    }

    private void Leave(Flight flight)
    {
        lock (_gate)
        {
            flight.Waiters--;
            if (flight.Waiters > 0 || flight.Task.IsCompleted)
            {
                return;
            }

            // why: no one is waiting any more; stop the read, and let the next caller start a fresh one instead of joining it
            if (ReferenceEquals(_current, flight))
            {
                _current = null;
            }

            flight.Stop.Cancel();
        }
    }

    private void Land(Flight flight)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_current, flight))
            {
                _current = null;
            }

            // why: inside the gate, so a last caller leaving can't cancel it after it is disposed
            flight.Stop.Dispose();
        }
    }

    private sealed class Flight
    {
        public CancellationTokenSource Stop { get; } = new();

        public Task<T> Task { get; set; } = null!; // why: set right after construction, inside the gate

        public int Waiters { get; set; }
    }
}
