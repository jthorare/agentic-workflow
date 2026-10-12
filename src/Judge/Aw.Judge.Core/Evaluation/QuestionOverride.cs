using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>A question's settings from config.json, when it has an entry there.</summary>
/// <param name="Enabled">False escalates every call with 'question-disabled'.</param>
/// <param name="Threshold">Overrides the question's own threshold when set.</param>
/// <param name="Providers">Restricts and reorders the chain for this question; names not in the chain are dropped.</param>
public sealed record QuestionOverride(bool Enabled, double? Threshold = null, IReadOnlyList<ProviderName>? Providers = null)
{
    /// <summary>A question's entry in the config: its enabled flag, its threshold and its provider list.</summary>
    /// <param name="config">The effective config.</param>
    /// <param name="question">The question's name.</param>
    /// <returns>The override, or null when the config has no entry for the question (its own defaults apply).</returns>
    public static QuestionOverride? For(JudgeConfig config, string question)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Questions.TryGetValue(question, out var entry))
        {
            return null;
        }

        // The parser has already refused unknown names, so every listed name parses.
        IReadOnlyList<ProviderName>? providers = entry.Providers is { } names
            ? [.. names.Select(n => Aw.Judge.Core.Providers.ProviderNames.TryParse(n, out var p) ? (ProviderName?)p : null).OfType<ProviderName>()]
            : null;
        return new QuestionOverride(entry.Enabled, entry.Threshold, providers);
    }
}
