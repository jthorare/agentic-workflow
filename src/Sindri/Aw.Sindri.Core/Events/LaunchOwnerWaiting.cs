using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.owner-waiting' payload (skill flows spec §7): a skill block's session ended a turn without finishing, so the owner is needed. Key 'launch.owner-waiting:&lt;launchId&gt;:&lt;turnEndedAtMs&gt;', one per turn end; history only, the needs-you row is computed live.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Reason">'question', 'noResult', 'unreadable' or 'skillUpdated'.</param>
/// <param name="Text">The skill's question, redacted; null for every other reason.</param>
/// <param name="ResultPath">The result file the reader looked at.</param>
/// <param name="Detail">The parse error or the other skill's name; null otherwise.</param>
/// <param name="Instance">The workflow instance the launch belongs to, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
public sealed record LaunchOwnerWaiting(
    [property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text,
    string ResultPath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null,
    InstanceAddress? Instance = null, string? State = null, int? Attempt = null);
