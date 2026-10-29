using Aw.Context.Core.Sync;

namespace Aw.Context.Testing.Records;

/// <summary>
/// A fake upstream (Slack, GitHub, Linear, ...) behind a <see cref="RecordFeed"/>, for <see cref="RecordFeedResumeContract"/>: it holds a source bigger than one pull, counts every request the feed makes, logs the ones it served and, once told
/// to <see cref="InterruptAfter"/>, refuses every request past the allowance the way a rate limit does (the binding's own fake does the refusing: an empty page that names a reset time, an HTTP 429, ...). A binding calls
/// <see cref="Admit"/> at the top of every upstream request and refuses the request when it answers false.
/// </summary>
public abstract class ResumeUpstream
{
    private readonly List<string> _served = [];
    private int _allowed = int.MaxValue;

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

    /// <summary>Serves requests again.</summary>
    public void Lift() => _allowed = int.MaxValue;

    /// <summary>Counts a request and says whether it is served.</summary>
    /// <param name="request">Text that names what the request asks for; logged when it is served.</param>
    /// <returns>True to serve it, false to refuse it as rate limited.</returns>
    protected bool Admit(string request)
    {
        Attempts++;
        if (Attempts > _allowed)
        {
            Interrupted = true;
            return false;
        }

        _served.Add(request);
        return true;
    }
}
