using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'launch.takeover-opened' payload (issue 1004): a takeover tried to open the live session of the Step it took over, in the launcher.json viewer or the box's detected terminal.
/// The launches view takes <see cref="Viewer"/> as the launch's latest viewer outcome. Key 'launch.takeover-opened:&lt;nonce&gt;'.
/// </summary>
/// <param name="Launch">The live launch whose session it opened.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The instance that was taken over.</param>
/// <param name="Viewer">The viewer, its outcome and, when nothing opened, the reason (skipped: no usable viewer; failed: the viewer's code).</param>
/// <param name="Attach">The command that attaches to the live session.</param>
public sealed record LaunchTakeoverOpened([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, InstanceAddress Instance, LaunchViewer Viewer, IReadOnlyList<string> Attach);
