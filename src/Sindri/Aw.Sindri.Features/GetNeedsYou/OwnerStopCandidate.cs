using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>An open instance that waits at a human-verb wait: its view and the wait state of its pinned recipe.</summary>
/// <param name="View">The instance's view.</param>
/// <param name="State">The wait state it is in.</param>
internal sealed record OwnerStopCandidate(InstanceView View, RecipeState State);
