namespace Aw.Transcripts.Core.Turns;

/// <summary>Who a turn is from.</summary>
public enum TurnRole
{
    /// <summary>A prompt a person typed (never tool output, harness text or another agent's message).</summary>
    Human,

    /// <summary>What the agent said and did until the next prompt.</summary>
    Assistant,
}
