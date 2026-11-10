using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Architecture.Tests;

/// <summary>
/// .agents/rules/csharp.md allows one source-generated JsonSerializerContext per package and one named exception.
/// A package with more than one context must have each extra one named in the rule, so a third can't slip in (#2496).
/// </summary>
[Trait("Lane", "integration")]
public sealed class CoreJsonContextRuleTests
{
    [Fact]
    public void Core_has_one_json_context_or_the_rule_names_each_one()
    {
        var contexts = typeof(StepArgs).Assembly.GetTypes()
            .Where(t => typeof(JsonSerializerContext).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var rule = File.ReadAllText(Path.Combine(Solution.RepoRoot, ".agents", "rules", "csharp.md"));

        // why: either fold Core to one context, or the rule names every context past the first.
        var unnamed = contexts.Where(c => !rule.Contains(c, StringComparison.Ordinal)).ToList();

        Assert.True(
            contexts.Count <= 1 || unnamed.Count == 0,
            $"Aw.Sindri.Core has {contexts.Count} JSON contexts ({string.Join(", ", contexts)}); csharp.md names none of: {string.Join(", ", unnamed)}");
    }
}
