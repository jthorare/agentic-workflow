using System.Globalization;
using System.Text;
using Aw.Judge.Core.Config;

namespace Aw.Judge.Testing;

/// <summary>A config as one comparable line (records hold dictionaries and lists, which compare by reference).</summary>
public static class ConfigText
{
    /// <summary>Describes every field of <paramref name="config"/>, in order.</summary>
    /// <param name="config">The config.</param>
    /// <returns>e.g. "wake-gate=True/0.7; providers=codex-cli|False; promptSort=…".</returns>
    public static string Of(JudgeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var text = new StringBuilder();
        foreach (var (name, q) in config.Questions)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{name}={q.Enabled}/{q.Threshold}/{List(q.Providers)}; ");
        }

        if (config.Providers is { } p)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"providers={List(p.AgentClis)}|{p.Jev}; ");
        }

        if (config.PromptSort is { } s)
        {
            var scaffolds = s.Scaffolds is null ? "-" : string.Join(',', s.Scaffolds.Select(static kv => $"{kv.Key}:{kv.Value}"));
            _ = text.Append(CultureInfo.InvariantCulture, $"promptSort={s.Enabled}|{s.BudgetMs}|{s.CooldownPrompts}|{scaffolds}");
        }

        return text.ToString();
    }

    private static string List(IReadOnlyList<string>? items) => items is null ? "-" : "[" + string.Join(',', items) + "]";
}
