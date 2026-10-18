using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'launch.started' payload (UI spec E-1, launcher L-10): the session exists, whatever its viewer did. Its public form,
/// <see cref="LaunchStartedPublic"/>, leaves out the viewer's stderr. Key 'launch.outcome:&lt;launchId&gt;', shared with 'launch.failed': the first wins.
/// </summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Mode">Open, attach or resume.</param>
/// <param name="Target">The agent that ran.</param>
/// <param name="SessionId">The agent's session id when known at start (bounded by the generic id pattern), otherwise null.</param>
/// <param name="TabTitle">The viewer's title ('sindri ITM-…').</param>
/// <param name="Viewer">What the viewer did.</param>
/// <param name="Attach">The command that attaches to the session.</param>
/// <param name="CatalogSource">Where the entry came from.</param>
/// <param name="ArgvHash">The hash of the entry's argv template.</param>
/// <param name="Sandbox">The sandbox summary (sandbox spec §8); left out when the launch was not sandboxed.</param>
public sealed record LaunchStarted(
    LaunchId Launch, ItemId Item, LaunchMode Mode, AgentId Target, SessionId? SessionId, string TabTitle, LaunchViewer Viewer,
    IReadOnlyList<string> Attach, CatalogSource CatalogSource, string ArgvHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SandboxSummary? Sandbox = null);
