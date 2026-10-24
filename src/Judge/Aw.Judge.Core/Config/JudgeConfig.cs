using System.Text.Json.Serialization;
using Aw.Judge.Core.Errors;
using Aw.Kernel;

namespace Aw.Judge.Core.Config;

/// <summary>The judge's config.json: per-question settings, the provider selection and the prompt sorter's overrides.</summary>
/// <param name="Questions">The questions' settings by name (required in the file); the parser adds wake-gate when it is missing.</param>
/// <param name="Providers">The provider selection, when the file sets one.</param>
/// <param name="PromptSort">The prompt sorter's overrides, when the file sets them.</param>
public sealed record JudgeConfig(
    IReadOnlyDictionary<string, QuestionConfig> Questions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProvidersConfig? Providers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PromptSortConfig? PromptSort = null)
{
    /// <summary>The question every config holds.</summary>
    public const string WakeGate = "wake-gate";

    /// <summary>A question's settings when config.json doesn't name it.</summary>
    public static QuestionConfig DefaultQuestion { get; } = new(Enabled: true, Threshold: 0.7);

    /// <summary>The config with no file: wake-gate enabled at 0.7.</summary>
    public static JudgeConfig Default { get; } = new(
        new Dictionary<string, QuestionConfig>(StringComparer.Ordinal) { [WakeGate] = DefaultQuestion });

    /// <summary>Sets one question's enabled flag; a question the config doesn't name gets an entry with no threshold, so
    /// it keeps its own. The providers and promptSort blocks are kept.</summary>
    /// <param name="question">The question name.</param>
    /// <param name="enabled">The flag.</param>
    /// <returns>The new config.</returns>
    public JudgeConfig SetEnabled(string question, bool enabled) => With(question, q => q with { Enabled = enabled });

    /// <summary>Sets one question's threshold, as <see cref="SetEnabled"/> does its flag.</summary>
    /// <param name="question">The question name.</param>
    /// <param name="threshold">The threshold, 0 to 1.</param>
    /// <returns>The new config, or AW-JUDGE-001 when the threshold is outside 0 to 1.</returns>
    public Result<JudgeConfig> SetThreshold(string question, double threshold) =>
        JudgeConfigParser.ThresholdProblem(threshold) is { } problem
            ? JudgeErrors.Invalid.With(problem)
            : With(question, q => q with { Threshold = threshold });

    /// <summary>Sets 'promptSort.enabled'; the rest of the promptSort block and every other key is kept.</summary>
    /// <param name="enabled">Whether the prompt sorter runs.</param>
    /// <returns>The new config.</returns>
    public JudgeConfig SetPromptSortEnabled(bool enabled) =>
        this with { PromptSort = (PromptSort ?? new PromptSortConfig()) with { Enabled = enabled } };

    /// <summary>Switches one scaffold in 'promptSort.scaffolds'; the other scaffolds and keys are kept.</summary>
    /// <param name="scaffold">The scaffold id: brief, bugfix, ui-evidence or plan-first.</param>
    /// <param name="on">Whether it fires.</param>
    /// <returns>The new config, or AW-JUDGE-001 when the scaffold is not one of the four.</returns>
    public Result<JudgeConfig> SetScaffold(string scaffold, bool on)
    {
        ArgumentNullException.ThrowIfNull(scaffold);
        if (!ProviderNames.Scaffolds.Contains(scaffold, StringComparer.Ordinal))
        {
            return JudgeErrors.Invalid.With($"unknown scaffold '{scaffold}' (known: {string.Join(", ", ProviderNames.Scaffolds)})");
        }

        var current = PromptSort ?? new PromptSortConfig();
        var scaffolds = new Dictionary<string, bool>(current.Scaffolds ?? new Dictionary<string, bool>(StringComparer.Ordinal), StringComparer.Ordinal) { [scaffold] = on };
        return this with { PromptSort = current with { Scaffolds = scaffolds } };
    }

    private JudgeConfig With(string question, Func<QuestionConfig, QuestionConfig> change)
    {
        ArgumentNullException.ThrowIfNull(question);
        var existing = Questions.TryGetValue(question, out var current) ? current : new QuestionConfig(Enabled: true);
        return this with { Questions = new Dictionary<string, QuestionConfig>(Questions, StringComparer.Ordinal) { [question] = change(existing) } };
    }
}
