using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Architecture.Tests;

/// <summary>
/// .agents/rules/csharp.md allows one source-generated JsonSerializerContext per package and two named exceptions.
/// Core's contexts past the first must be on a reviewed list that the rule names, so a new one can't slip in (#2496, #2563).
/// </summary>
[Trait("Lane", "integration")]
public sealed class CoreJsonContextRuleTests
{
    private static readonly string _rule = File.ReadAllText(Path.Combine(Solution.RepoRoot, ".agents", "rules", "csharp.md"));

    [Fact]
    public void Core_has_one_json_context_or_the_rule_names_each_one()
    {
        var contexts = typeof(StepArgs).Assembly.GetTypes()
            .Where(t => typeof(JsonSerializerContext).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        var violations = CoreJsonContextRule.Violations(contexts, _rule);

        Assert.True(
            violations.Count == 0,
            $"Aw.Sindri.Core has {contexts.Count} JSON contexts ({string.Join(", ", contexts)}); not allowed by csharp.md: {string.Join(", ", violations)}");
    }

    [Fact]
    public void A_context_the_rule_names_only_for_another_exception_is_refused()
    {
        // why: ProviderWireJsonContext is in csharp.md for Judge's Infrastructure exception, not as a Core context.
        var violations = CoreJsonContextRule.Violations(["GraphJsonContext", "GraphOpJsonContext", "ProviderWireJsonContext"], _rule);

        Assert.Equal(["ProviderWireJsonContext"], violations);
    }

    [Fact]
    public void A_context_whose_name_is_part_of_an_allowed_one_is_refused()
    {
        // why: "JsonContext" is a substring of "GraphJsonContext", but no Core context of that name was reviewed.
        var violations = CoreJsonContextRule.Violations(["GraphJsonContext", "GraphOpJsonContext", "JsonContext"], _rule);

        Assert.Equal(["JsonContext"], violations);
    }

    [Fact]
    public void An_allowed_context_the_rule_no_longer_names_is_refused()
    {
        var rule = _rule.Replace("GraphOpJsonContext", "SomethingElse", StringComparison.Ordinal);

        var violations = CoreJsonContextRule.Violations(["GraphJsonContext", "GraphOpJsonContext"], rule);

        Assert.Equal(["GraphOpJsonContext"], violations);
    }
}
