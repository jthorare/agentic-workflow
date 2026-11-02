using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>One agent session that sat idle and was ended with no report, for a workflow Step no newer launch took over.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Instance">The workflow instance whose Step it ran.</param>
/// <param name="State">The Step's state id, when the launch row has it.</param>
/// <param name="EndedAt">When the session was ended.</param>
/// <param name="Item">The item the launch belongs to.</param>
public sealed record StalledLaunch(LaunchId Launch, InstanceAddress Instance, string? State, DateTimeOffset EndedAt, ItemId Item);
