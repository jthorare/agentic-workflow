using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A 'waitFor: { child }' (spec §3.4): the parent starts a child of <paramref name="Recipe"/> and waits for its outcome.</summary>
/// <param name="Recipe">The child's recipe.</param>
/// <param name="Inputs">The child's input facts, each a literal or '{ fact }' of the parent.</param>
/// <param name="OnParked">Whether a child's park propagates to the parent.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildWait(RecipeId Recipe, IReadOnlyDictionary<FactKey, WithValue> Inputs, OnParked OnParked);
