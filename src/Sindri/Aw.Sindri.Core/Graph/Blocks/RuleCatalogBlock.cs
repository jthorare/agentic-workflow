using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'rule_catalog' block (issue 3787): hands on every rule record of the repo with its checks, so a prompt can see which rules and check ids already exist.</summary>
/// <param name="catalog">Where the rules are read.</param>
public sealed class RuleCatalogBlock(IRuleCatalogPort catalog) : IGraphBlock
{
    /// <summary>The most characters of a rule's text handed on; the longest record today has 473.</summary>
    public const int TextChars = 600;

    /// <summary>The most UTF-8 bytes the whole output may take as the engine stores it (JSON); the engine cuts a node's output at 64 KB, so this leaves 4 KB of room.</summary>
    public const int OutputBudgetBytes = 60_000;

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rules"] = "array",
        ["count"] = "number",
        ["skipped"] = "number",
        ["more"] = "number",
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

        var readable = listed.Value.Rules;
        var sections = new JsonArray();
        foreach (var section in readable.Select(static r => r.Section).Where(static s => s.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            sections.Add(section);   // why: every readable record's section, kept or not, so a later vet knows them all
        }

        var rules = new JsonArray();
        var bytes = FrameBytes(sections, listed.Value.Skipped);
        foreach (var rule in readable)
        {
            var json = RuleJson(rule);
            bytes += Encoding.UTF8.GetByteCount(json.ToJsonString()) + 1;   // +1 for the comma between array items
            if (bytes > OutputBudgetBytes)
            {
                break;
            }

            rules.Add((JsonNode)json);
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["rules"] = rules,
            ["count"] = rules.Count,
            ["skipped"] = listed.Value.Skipped,
            ["more"] = readable.Count - rules.Count,
            ["sections"] = sections,
        });
    }

    // why: the output as the engine stores it (JsonObject.ToJsonString, UTF-8) with no rules and the largest numbers, so the rules add up on top of it
    private static int FrameBytes(JsonArray sections, int skipped)
    {
        var frame = new JsonObject
        {
            ["rules"] = new JsonArray(),
            ["count"] = int.MaxValue,
            ["skipped"] = skipped,
            ["more"] = int.MaxValue,
            ["sections"] = sections.DeepClone(),
        };
        return Encoding.UTF8.GetByteCount(frame.ToJsonString());
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
