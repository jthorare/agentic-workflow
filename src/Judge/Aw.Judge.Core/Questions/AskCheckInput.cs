namespace Aw.Judge.Core.Questions;

/// <summary>ask-check's input (hook contract: done-gate.sh sends the agent's last turn).</summary>
/// <param name="TranscriptTail">The tail of the transcript.</param>
public sealed record AskCheckInput(string TranscriptTail);
