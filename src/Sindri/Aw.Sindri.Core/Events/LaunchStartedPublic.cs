using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'launch.started': the payload with <see cref="LaunchViewerPublic"/>, so the viewer's stderr never leaves the ledger.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Mode">Open, attach or resume.</param>
/// <param name="Target">The agent that ran.</param>
/// <param name="SessionId">The agent's session id when known at start, otherwise null.</param>
/// <param name="TabTitle">The viewer's title.</param>
/// <param name="Viewer">What the viewer did, without its stderr.</param>
/// <param name="Attach">The command that attaches to the session.</param>
/// <param name="CatalogSource">Where the entry came from.</param>
/// <param name="ArgvHash">The hash of the entry's argv template.</param>
/// <param name="Instance">The workflow instance an engine launch belongs to, or null (attended and click launches, and events recorded before this field).</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number ('{entry}.{n}' → n), or null.</param>
/// <param name="Sandbox">The sandbox summary (sandbox spec §8); left out when the launch was not sandboxed.</param>
/// <param name="Model">The model the launch asked for (model.picked says how it was chosen), or null when none was named and for events recorded before this field.</param>
public sealed record LaunchStartedPublic(
    [property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, LaunchMode Mode, AgentId Target, SessionId? SessionId, string TabTitle, LaunchViewerPublic Viewer,
    IReadOnlyList<string> Attach, CatalogSource CatalogSource, string ArgvHash,
    InstanceAddress? Instance = null, string? State = null, int? Attempt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxSummary? Sandbox = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model = null);
