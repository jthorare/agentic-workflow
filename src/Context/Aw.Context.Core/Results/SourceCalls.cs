using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Results;

/// <summary>
/// Calls one source under the query's deadline: an error or a missed deadline makes it degraded for this query
/// (SND-CONTEXT-010), never an exception the query sees. Cancellation of the query itself still propagates.
/// </summary>
public static class SourceCalls
{
    /// <summary>Runs one call to a source within a deadline.</summary>
    /// <typeparam name="T">The call's answer.</typeparam>
    /// <param name="source">The source called.</param>
    /// <param name="deadline">What is left of the deadline and the clock it runs on.</param>
    /// <param name="call">The call, given a token that expires with the deadline.</param>
    /// <param name="cancellationToken">Cancels the whole query.</param>
    /// <returns>The answer, or SND-CONTEXT-010 when the deadline passed.</returns>
    public static async ValueTask<Result<T>> Within<T>(IContextSource source, (TimeSpan Left, TimeProvider Clock) deadline,
        Func<CancellationToken, ValueTask<Result<T>>> call, CancellationToken cancellationToken)
    {
        using var expiry = new CancellationTokenSource(deadline.Left <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : deadline.Left, deadline.Clock);   // the query's clock
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);
        try
        {
            return await call(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ContextErrors.MissedDeadline(source.Describe().Id.Value);
        }
    }

    /// <summary>The degraded entry for a source whose call failed.</summary>
    /// <param name="source">The source.</param>
    /// <param name="error">What the call failed with.</param>
    /// <param name="since">When it failed.</param>
    /// <returns>The entry, with the cause when it isn't 010 itself.</returns>
    public static DegradedSource Degraded(SourceId source, Error error, DateTimeOffset since) =>
        new(source, ContextErrors.SourceUnavailable.Code, error.Code == ContextErrors.SourceUnavailable.Code ? error.Message : $"{error.Code}: {error.Message}", since);   // the cause, when it isn't 010 itself
}
