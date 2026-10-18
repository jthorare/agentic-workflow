using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'item.open-requested' payload (UI spec E-1): a person asked to open an item's session. The launcher service answers it with 'launch.started' or 'launch.failed'.</summary>
/// <param name="Launch">The request's launch id.</param>
/// <param name="Item">The item.</param>
/// <param name="Mode">Open, attach or resume.</param>
/// <param name="Target">The agent to run (checked against the pattern at the request, against the catalog at the launch).</param>
/// <param name="ChosenBy">'default' when the person took the remembered target, 'click' when they picked one.</param>
public sealed record ItemOpenRequested([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, LaunchMode Mode, AgentId Target, string ChosenBy);
