using System.Globalization;
using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Judge.Core.Config;

/// <summary>
/// Reads config.json: deserialized into <see cref="JudgeConfig"/> through the AwJson profile (the records' required
/// and nullable members are the schema; unknown keys are ignored), then checked. Anything that fails either step is
/// AW-JUDGE-007 with the reason; there is no per-field leniency. A config without wake-gate gets the default one.
/// </summary>
public static class JudgeConfigParser
{
    /// <summary>The prompt sorter's budget range: the hook's 1500 ms kill minus the 450 ms startup allowance at most.</summary>
    public const double MinBudgetMs = 200;

    /// <summary>The upper end of the prompt sorter's budget, in milliseconds.</summary>
    public const double MaxBudgetMs = 1050;

    /// <summary>Parses config.json's bytes.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="json">The package's AwJson options (its context knows JudgeConfig).</param>
    /// <returns>The config, or AW-JUDGE-007 saying what is wrong.</returns>
    public static Result<JudgeConfig> Parse(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions json)
    {
        var read = AwJson.Deserialize<JudgeConfig>(utf8Json, json);
        if (read.IsError)
        {
            return JudgeErrors.ConfigMalformed.With(read.Error.Message);
        }

        var problem = Problem(read.Value);
        return problem is null ? WithWakeGate(read.Value) : JudgeErrors.ConfigMalformed.With(problem);
    }

    /// <summary>What makes a question's threshold invalid, or null when it is a confidence between 0 and 1.</summary>
    /// <param name="threshold">The threshold.</param>
    /// <returns>The problem, or null.</returns>
    public static string? ThresholdProblem(double threshold) =>
        double.IsFinite(threshold) && threshold is >= 0 and <= 1 ? null : string.Create(CultureInfo.InvariantCulture, $"threshold must be between 0 and 1, not {threshold}");

    private static string? Problem(JudgeConfig config) =>
        config.Questions.Select(static q => QuestionProblem(q.Key, q.Value)).FirstOrDefault(static p => p is not null)
        ?? ProvidersProblem(config.Providers)
        ?? PromptSortProblem(config.PromptSort);

    private static string? QuestionProblem(string name, QuestionConfig? question)
    {
        if (name.Length == 0)
        {
            return "a question name is empty";
        }

        if (question is null)
        {
            return $"question '{name}' is null";
        }

        return (question.Threshold is { } value && ThresholdProblem(value) is { } threshold ? $"question '{name}': {threshold}" : null)
            ?? Unknown($"question '{name}' providers", question.Providers, ProviderNames.All);
    }

    private static string? ProvidersProblem(ProvidersConfig? providers) =>
        providers is null ? null
        : Unknown("providers.agentClis", providers.AgentClis, ProviderNames.AgentClis)
            ?? (providers.InputCapChars is { } cap and not (>= ProvidersConfig.MinInputCapChars and <= ProvidersConfig.MaxInputCapChars)
                ? string.Create(CultureInfo.InvariantCulture, $"providers.inputCapChars must be between {ProvidersConfig.MinInputCapChars} and {ProvidersConfig.MaxInputCapChars}, not {cap}")
                : null);

    private static string? PromptSortProblem(PromptSortConfig? sort)
    {
        if (sort is null)
        {
            return null;
        }

        if (sort.BudgetMs is { } budget && budget is not (>= MinBudgetMs and <= MaxBudgetMs))
        {
            return string.Create(CultureInfo.InvariantCulture, $"promptSort.budgetMs must be between {MinBudgetMs} and {MaxBudgetMs}, not {budget}");
        }

        if (sort.CooldownPrompts is { } cooldown && (cooldown < 0 || cooldown != Math.Floor(cooldown)))
        {
            return string.Create(CultureInfo.InvariantCulture, $"promptSort.cooldownPrompts must be a whole number of at least 0, not {cooldown}");
        }

        return sort.Scaffolds is null ? null : Unknown("promptSort.scaffolds", [.. sort.Scaffolds.Keys], ProviderNames.Scaffolds);
    }

    private static string? Unknown(string where, IReadOnlyList<string>? names, IReadOnlyList<string> known) =>
        names?.FirstOrDefault(n => !known.Contains(n, StringComparer.Ordinal)) is { } bad
            ? $"{where}: unknown name '{bad}' (known: {string.Join(", ", known)})"
            : null;

    private static JudgeConfig WithWakeGate(JudgeConfig config) =>
        config.Questions.ContainsKey(JudgeConfig.WakeGate)
            ? config
            : config with { Questions = new Dictionary<string, QuestionConfig>(config.Questions, StringComparer.Ordinal) { [JudgeConfig.WakeGate] = JudgeConfig.DefaultQuestion } };
}
