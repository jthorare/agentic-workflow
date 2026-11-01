using Aw.Context.Core.Sync;

namespace Aw.Context.Testing.Records;

/// <summary>
/// A fake upstream (Slack, GitHub, Linear, ...) behind a <see cref="RecordFeed"/>, for <see cref="RecordFeedResumeContract"/>: it holds a source bigger than one pull, counts every request the feed makes, logs the ones it served and, once told
/// to <see cref="InterruptAfter"/>, refuses every request past the allowance the way a rate limit does (the binding's own fake does the refusing: an empty page that names a reset time, an HTTP 429, ...). Told to
/// <see cref="CancelAfter"/> instead, it cancels a token source at the first request past the allowance and throws <see cref="OperationCanceledException"/>, the way an HTTP call under a cancelled token does (the feed's time cap). A binding calls
/// <see cref="Admit"/> at the top of every upstream request and refuses the request when it answers false.
/// </summary>
public abstract class ResumeUpstream
{
    private readonly List<string> _served = [];
    private int _allowed = int.MaxValue;
    private CancellationTokenSource? _cancels;

    /// <summary>Gets the feed under test, reading this upstream.</summary>
    public abstract RecordFeed Feed { get; }

    /// <summary>Gets the key of every record the source holds; more than one pull's worth.</summary>
    public abstract IReadOnlyCollection<string> Keys { get; }

    /// <summary>Gets how many upstream requests the feed has made, served or refused.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets a value indicating whether a request was ever refused.</summary>
    public bool Interrupted { get; private set; }

    /// <summary>Gets the requests the upstream served, in order, each as text that names what it asked for (a page's cursor, a thread's id).</summary>
    public IReadOnlyList<string> Served => _served;

    /// <summary>Refuses every request after the next <paramref name="requests"/> ones, as a rate limit would.</summary>
    /// <param name="requests">How many more requests are served.</param>
    public void InterruptAfter(int requests) => _allowed = Attempts + requests;

    /// <summary>Cancels <paramref name="source"/> at the first request after the next <paramref name="requests"/> ones, and throws <see cref="OperationCanceledException"/> from it, as an HTTP call under a cancelled token does.</summary>
    /// <param name="requests">How many more requests are served.</param>
    /// <param name="source">The source whose token the pulls run under.</param>
    public void CancelAfter(int requests, CancellationTokenSource source)
    {
        _allowed = Attempts + requests;
        _cancels = source;
    }

    /// <summary>Serves requests again; a cancellation set up by <see cref="CancelAfter"/> is dropped.</summary>
    public void Lift()
    {
        _allowed = int.MaxValue;
        _cancels = null;
    }

    /// <summary>Counts a request and says whether it is served.</summary>
    /// <param name="request">Text that names what the request asks for; logged when it is served.</param>
    /// <returns>True to serve it, false to refuse it as rate limited. When a cancellation is set up it cancels the source and throws instead.</returns>
    protected bool Admit(string request)
    {
        Attempts++;
        if (Attempts > _allowed)
        {
            Interrupted = true;
            if (_cancels is { } source)
            {
                source.Cancel();
                throw new OperationCanceledException(source.Token);
            }

            return false;
        }

        _served.Add(request);
        return true;
    }
}
