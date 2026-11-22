using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'skill_catalog' block (issue 3787, piece 4): hands on every skill of the toolkit with its description, so a prompt can pick the skill a finding is about.</summary>
/// <param name="catalog">Where the skills are read.</param>
public sealed class SkillCatalogBlock(ISkillCatalogPort catalog) : IGraphBlock
{
    /// <summary>The most characters of a description handed on; 200 skills of this size stay well under the 64 KB node output cap.</summary>
    public const int DescriptionChars = 300;

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["skills"] = "array",
        ["count"] = "number",
    });

    /// <inheritdoc />
    public string Type => "skill_catalog";

    /// <inheritdoc />
    public string Title => "Skill catalog";

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

        var skills = new JsonArray();
        foreach (var skill in listed.Value)
        {
            var cut = skill.Description.Length > DescriptionChars && char.IsHighSurrogate(skill.Description[DescriptionChars - 1]) ? DescriptionChars - 1 : DescriptionChars;
            var description = skill.Description.Length > cut ? skill.Description[..cut] : skill.Description;
            skills.Add((JsonNode)new JsonObject { ["name"] = skill.Name, ["description"] = description });
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["skills"] = skills, ["count"] = listed.Value.Count });
    }
}
