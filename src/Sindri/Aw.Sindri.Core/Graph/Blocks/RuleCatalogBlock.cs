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
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = catalog;
        throw new NotSupportedException("red: rule_catalog is not built yet");
    }
}
