using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe input in its long form (spec §3.2): '{ key, label?, type? }'; the short form is the bare key.</summary>
/// <param name="Key">The fact the start supplies.</param>
/// <param name="Label">The start form's label.</param>
/// <param name="Type">How the start form asks for it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInput(FactKey Key, string? Label, InputType? Type);
