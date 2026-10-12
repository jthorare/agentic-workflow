namespace Aw.Judge.Host.Commands;

/// <summary>An escalated question's stdout (exit 2; ask-check exits 0).</summary>
/// <param name="Escalated">Always true: tells the envelope from a decided one.</param>
/// <param name="ReasonCode">'question-disabled', 'invalid-input', 'rule-timeout', 'below-threshold' or 'no-provider-decided'.</param>
internal sealed record EscalatedEnvelope(bool Escalated, string ReasonCode)
{
    /// <summary>The envelope for an escalation.</summary>
    internal static EscalatedEnvelope For(string reasonCode) => new(Escalated: true, reasonCode);
}
