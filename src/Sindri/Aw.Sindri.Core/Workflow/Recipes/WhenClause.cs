using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A transition's closed condition (spec §3.6): exactly one of '{ outcome }', '{ fact, is }', '{ loop, below }',
/// '{ all }', '{ any }' or '{ not }'. One record with optional fields (W3); the rules check that exactly one form is set.
/// </summary>
/// <param name="Outcome">Holds when the trigger's source reported this outcome.</param>
/// <param name="Fact">With <paramref name="Is"/>: holds when the fact is present or absent.</param>
/// <param name="Is">Present or absent, for <paramref name="Fact"/>.</param>
/// <param name="Loop">With <paramref name="Below"/>: holds while the named loop's round is below its cap.</param>
/// <param name="Below">The bound, for <paramref name="Loop"/>.</param>
/// <param name="All">Holds when every clause holds.</param>
/// <param name="Any">Holds when at least one clause holds.</param>
/// <param name="Not">Holds when the clause doesn't.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WhenClause(
    OutcomeName? Outcome,
    FactKey? Fact,
    FactPresence? Is,
    string? Loop,
    LoopBound? Below,
    IReadOnlyList<WhenClause>? All,
    IReadOnlyList<WhenClause>? Any,
    WhenClause? Not);
