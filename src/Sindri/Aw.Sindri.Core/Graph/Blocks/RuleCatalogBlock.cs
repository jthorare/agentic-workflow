using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'rule_catalog' block (issue 3787): hands on every rule record of the repo with its checks, so a prompt can see which rules and check ids already exist.</summary>
/// <param name="catalog">Where the rules are read.</param>
public sealed class RuleCatalogBlock(IRuleCatalogPort catalog) : IGraphBlock
{
    /// <summary>The most rules handed on, taken in id order.</summary>
    public const int MaxRules = 500;

    /// <summary>The most characters of a rule's text handed on; the longest record today has 473.</summary>
    public const int TextChars = 600;

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rules"] = "array",
        ["count"] = "number",
        ["skipped"] = "number",
        ["sections"] = "array",
    });

    /// <inheritdoc />
    public string Type => "rule_catalog";

    /// <inheritdoc />
    public string Title => "Rule catalog";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([]);

    /// <inheritdoc />
    public string Example => "{}";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var listed = await catalog.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var taken = listed.Value.Rules.Take(MaxRules).ToList();
        var rules = new JsonArray();
        foreach (var rule in taken)
        {
            rules.Add((JsonNode)RuleJson(rule));
        }

        var sections = new JsonArray();
        foreach (var section in taken.Select(static r => r.Section).Where(static s => s.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            sections.Add(section);
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["rules"] = rules,
            ["count"] = taken.Count,
            ["skipped"] = listed.Value.Skipped,
            ["sections"] = sections,
        });
    }

    private static JsonObject RuleJson(RuleListing rule)
    {
        var checks = new JsonArray();
        foreach (var check in rule.Checks)
        {
            checks.Add((JsonNode)new JsonObject { ["type"] = check.Type, ["id"] = check.Id });
        }

        var cut = rule.Text.Length > TextChars && char.IsHighSurrogate(rule.Text[TextChars - 1]) ? TextChars - 1 : TextChars;
        return new JsonObject
        {
            ["id"] = rule.Id,
            ["kind"] = rule.Kind,
            ["section"] = rule.Section,
            ["text"] = rule.Text.Length > cut ? rule.Text[..cut] : rule.Text,
            ["status"] = rule.Status,
            ["checks"] = checks,
        };
    }
}
