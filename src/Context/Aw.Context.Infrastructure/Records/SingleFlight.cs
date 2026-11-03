namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// One run of a read at a time: callers that arrive while it runs wait on the same run (#1951). The run stops only when
/// every caller has gone; the next call after that, or after it ends, starts a new one.
/// </summary>
/// <typeparam name="T">What the read returns.</typeparam>
internal sealed class SingleFlight<T>
{
    /// <summary>Runs <paramref name="work"/>, or joins the run already in flight.</summary>
    /// <param name="work">The read; its token is cancelled when every caller has gone.</param>
    /// <param name="cancellationToken">Stops this caller's wait.</param>
    /// <returns>The run's value.</returns>
    public ValueTask<T> Run(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
