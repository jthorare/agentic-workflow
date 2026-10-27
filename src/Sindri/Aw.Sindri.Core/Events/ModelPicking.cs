using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'model.picking' payload (model-pick spec §6): appended just before Jev is asked, so the app can show "choosing model…". Key 'model.picking:&lt;launchId&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The workflow instance.</param>
/// <param name="State">The Step's state id.</param>
/// <param name="Attempt">The Step run's number.</param>
/// <param name="StartedAt">When the pick started.</param>
public sealed record ModelPicking([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, InstanceAddress Instance, string State, int Attempt, DateTimeOffset StartedAt);
