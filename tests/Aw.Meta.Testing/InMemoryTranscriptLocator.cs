using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ITranscriptLocator: a session's transcript is the stored transcript whose file name is &lt;session&gt;.jsonl.</summary>
/// <param name="transcripts">The transcripts that exist.</param>
public sealed class InMemoryTranscriptLocator(InMemoryTranscriptReader transcripts) : ITranscriptLocator
{
    /// <inheritdoc />
    public ValueTask<Result<string?>> Find(SessionId session, CancellationToken cancellationToken)
    {
        var suffix = "/" + session.Value + ".jsonl";
        return Done.Of<string?>(transcripts.Paths.FirstOrDefault(p => p.EndsWith(suffix, StringComparison.Ordinal)));
    }
}
