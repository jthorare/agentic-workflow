namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The text a skill watch fails a Step with, "SND-SKILL-004: detail", taken apart for the step.failed it becomes (skill flows spec §3.3).</summary>
internal static class SkillFailureText
{
    /// <summary>Splits at the first ": ".</summary>
    /// <param name="failure">The failure text.</param>
    /// <returns>The code before the first ": " and the reason after it; no code and the whole text as the reason when there is no ": ".</returns>
    internal static (string? Code, string Reason) Split(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var split = failure.IndexOf(": ", StringComparison.Ordinal);
        return split < 0 ? (null, failure) : (failure[..split], failure[(split + 2)..]);
    }
}
