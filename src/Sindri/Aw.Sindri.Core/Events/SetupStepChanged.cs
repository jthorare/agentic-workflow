using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'setup.step.changed' payload (setup-once spec §7.5): the derived state of a step differs from the last event of that step. Key 'setup.step.changed:&lt;id&gt;:&lt;state&gt;:&lt;n&gt;'.</summary>
/// <param name="CommandId">The id of the service run that recorded it.</param>
/// <param name="Step">The step ('repo', 'profile', 'recipes', 'mcp', 'map' or 'sources'); text, because the step types sit in a namespace that reads events.</param>
/// <param name="State">The state it moved to ('todo', 'running', 'needs-you', 'done', 'failed' or 'skipped').</param>
/// <param name="Code">The error code when the step failed, else null.</param>
public sealed record SetupStepChanged(CommandId CommandId, [property: JsonPropertyName("id")] string Step, string State, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code = null);
