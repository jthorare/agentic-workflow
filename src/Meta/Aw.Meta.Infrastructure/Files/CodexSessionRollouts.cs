using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>ICodexRollouts over ~/.codex/sessions: the rollout of a session is rollout-*-&lt;session&gt;.jsonl under a year/month/day folder.</summary>
/// <param name="sessions">The sessions directory.</param>
internal sealed class CodexSessionRollouts(string sessions) : ICodexRollouts
{
    public ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken) => throw new NotSupportedException(sessions);
}
