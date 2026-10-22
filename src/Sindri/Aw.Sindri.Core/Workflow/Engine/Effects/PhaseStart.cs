using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>What <see cref="IPhaseStarts.Start"/> is asked to start.</summary>
/// <param name="Line">The line instance that asks; the linked instance's parent.</param>
/// <param name="Recipe">The recipe to start, one the line pinned ('workflow.phase').</param>
/// <param name="Phase">The phase's id in the plan.</param>
/// <param name="Attempt">The phase's attempt, from 1: an owner's retry is the next.</param>
/// <param name="Item">The line's item.</param>
/// <param name="Inputs">The input facts by key, each already stored.</param>
public sealed record PhaseStart(InstanceAddress Line, RecipeId Recipe, string Phase, int Attempt, ItemKey Item, IReadOnlyDictionary<FactKey, FactValue> Inputs);
