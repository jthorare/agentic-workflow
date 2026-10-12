using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A recipe in its normalized long form (spec §3.2, W3): what the reader binds after turning short forms into long
/// ones, what the JSON Schema describes and what the hash covers (without <see cref="Hash"/>). Collections are never
/// null in the long form: an absent list or map is empty.
/// </summary>
/// <param name="Schema">The schema version; 1.</param>
/// <param name="Id">The recipe's id, 'workflow.&lt;name&gt;'.</param>
/// <param name="Version">The author's version number (RCP-VERSION-001/002).</param>
/// <param name="Description">What the recipe is for.</param>
/// <param name="Base">The recipe a generated recipe was made from (written by Sindri's generator; RCP-CAP-004 on a hand-written one).</param>
/// <param name="Inputs">The facts a start supplies, with how the app's start form asks for them.</param>
/// <param name="Initial">The state an instance starts in: an active or wait state.</param>
/// <param name="States">The states by name, in file order.</param>
/// <param name="Transitions">The transitions, in file order (the first whose 'when' holds is the matched one).</param>
/// <param name="Loops">The loops by name.</param>
/// <param name="Budget">The recipe-wide budget.</param>
/// <param name="Evals">The eval files, relative to the recipe's directory (spec §9.2).</param>
/// <param name="Hash">The hash the file states, checked when present (RCP-HASH-001).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Recipe(
    int Schema,
    RecipeId Id,
    int Version,
    string? Description,
    RecipeRef? Base,
    IReadOnlyList<RecipeInput> Inputs,
    string Initial,
    IReadOnlyDictionary<string, RecipeState> States,
    IReadOnlyList<RecipeTransition> Transitions,
    IReadOnlyDictionary<string, LoopSpec> Loops,
    BudgetSpec? Budget,
    IReadOnlyList<string> Evals,
    RecipeHash? Hash);
