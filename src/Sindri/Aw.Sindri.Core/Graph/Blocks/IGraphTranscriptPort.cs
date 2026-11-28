using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// Where 'transcript_turns' and 'transcript_batch' read the local agent transcripts (issue 3787). Read-only. Sindri Core can't reference Aw.Transcripts, so the
/// Infrastructure adapter maps this onto its corpus. Every text it returns has been through the transcript scrubber; the blocks still treat it as untrusted.
/// </summary>
public interface IGraphTranscriptPort
{
    /// <summary>The human turns written at or after <see cref="TranscriptTurnQuery.Since"/>, newest first. Subagent sessions are left out: their prompts come from the parent agent, not a person.</summary>
    /// <param name="query">The time and filters.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The turns' refs; none when no transcript matches.</returns>
    public ValueTask<Result<IReadOnlyList<HumanTurnRef>>> HumanTurns(TranscriptTurnQuery query, CancellationToken cancellationToken);

    /// <summary>The text of each human turn in <paramref name="refs"/>, in the order asked. A ref no transcript has is left out.</summary>
    /// <param name="refs">Turn refs, such as 'transcript:00000a1b2c3d#42'.</param>
    /// <param name="withBefore">Whether to return the text of the assistant turn just before each human turn, in the same session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The turns found.</returns>
    public ValueTask<Result<IReadOnlyList<TranscriptTurnText>>> Read(IReadOnlyList<string> refs, bool withBefore, CancellationToken cancellationToken);
}
