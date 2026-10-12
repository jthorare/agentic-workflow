namespace Aw.Judge.Core.Questions;

/// <summary>wake-gate's input (hook contract: send-gate.sh sends these two fields).</summary>
/// <param name="Text">The message an agent wants to send.</param>
/// <param name="SenderKind">Who sends it.</param>
public sealed record WakeGateInput(string Text, SenderKind SenderKind);
