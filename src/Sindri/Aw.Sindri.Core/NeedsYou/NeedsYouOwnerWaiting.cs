using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What a 'skillWaiting' row carries beside its sentence (skill flows spec §7): the same fields as the 'launch.owner-waiting' event it is computed from, so the row's Details can show the full text and the result path.</summary>
/// <param name="Launch">The skill launch whose session waits.</param>
/// <param name="Reason">'question', 'noResult', 'unreadable' or 'skillUpdated'.</param>
/// <param name="Text">The skill's question, redacted; null for every other reason.</param>
/// <param name="ResultPath">The result file the reader looked at.</param>
/// <param name="Detail">The parse error or the other skill's name, redacted; null otherwise.</param>
/// <param name="Instance">The workflow instance the launch belongs to, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
public sealed record NeedsYouOwnerWaiting(
    [property: JsonPropertyName("launchId")] LaunchId Launch, string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text,
    string ResultPath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null,
    InstanceAddress? Instance = null, string? State = null, int? Attempt = null);
