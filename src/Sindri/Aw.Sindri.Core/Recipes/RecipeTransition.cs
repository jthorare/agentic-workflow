using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>
/// One transition: from → to on a signal, with the gates that must pass, the evidence it needs and the effects it emits.
/// Transitions from one state on one signal are tried in order; the first whose gates pass is applied.
/// </summary>
/// <param name="From">The state it leaves.</param>
/// <param name="To">The state it enters.</param>
/// <param name="On">The signal that asks for it, for example 'passed' ('child.done' for a child's terminal event).</param>
/// <param name="Gates">Gate block ids, all of which must pass.</param>
/// <param name="Evidence">Evidence kinds, 'kind' or 'kind@head' / 'kind@base': registered for the instance and bound to that sha.</param>
/// <param name="Effects">Effect block ids, emitted as outbox commands in the transition's transaction.</param>
/// <param name="Loop">The loop this transition counts a round of.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeTransition(
    string From,
    string To,
    string On,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BlockId>? Gates = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Evidence = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BlockId>? Effects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Loop = null);
