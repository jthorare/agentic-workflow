using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>GET /sindri/launch/picks?item=&amp;instance= (model-pick spec §6.1): the model picks of an item's or an instance's runs, oldest first.</summary>
/// <param name="Item">Only this item's picks, or null for every item.</param>
/// <param name="Instance">Only this workflow instance's picks, or null for every instance.</param>
public sealed record GetLaunchPicksRequest(ItemRef? Item = null, InstanceAddress? Instance = null) : IQuery<LaunchPicks>;
