using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A block a recipe names with its arguments: an effect, or a Step input (spec §3.4, §3.5). The short form is the bare id.</summary>
/// <param name="Use">The block.</param>
/// <param name="With">Its arguments by parameter name.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BlockUse(BlockId Use, IReadOnlyDictionary<string, WithValue> With);
