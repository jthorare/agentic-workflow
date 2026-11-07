using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.turn-started' payload (skill flows spec §7, F-2a): a skill block's session that had raised a 'launch.owner-waiting' got a new prompt, so the owner is no longer needed. Key 'launch.turn-started:&lt;launchId&gt;:&lt;promptedAtMs&gt;', one per new turn; the waiting row clears on it.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The workflow instance the launch belongs to, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
public sealed record LaunchTurnStarted([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, InstanceAddress? Instance = null, string? State = null, int? Attempt = null);
