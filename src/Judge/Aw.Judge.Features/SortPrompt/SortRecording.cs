using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.PromptSort;

namespace Aw.Judge.Features.SortPrompt;

/// <summary>Everything the recorder keeps about one sorted prompt.</summary>
/// <param name="Prompt">The prompt as typed (only its digest is kept).</param>
/// <param name="Sent">The redacted, capped prompt (what Jev saw, and what the detail row holds).</param>
/// <param name="Session">The session, when the hook had one.</param>
/// <param name="Outcome">What the sort found.</param>
/// <param name="Plan">The scaffold plan.</param>
/// <param name="Latency">How long the sort took.</param>
internal sealed record SortRecording(string Prompt, string Sent, SessionId? Session, SortOutcome Outcome, ScaffoldPlan Plan, TimeSpan Latency)
{
    /// <summary>The images read for the prompt, in the read form the row keeps; the row's imagesDropped says whether they went to Jev.</summary>
    public IReadOnlyList<ImageAttachment> Images { get; init; } = [];

    /// <summary>What was dropped on the way, one line each; kept on the row as 'imagesDropped'.</summary>
    public IReadOnlyList<string> ImagesDropped { get; init; } = [];
}
