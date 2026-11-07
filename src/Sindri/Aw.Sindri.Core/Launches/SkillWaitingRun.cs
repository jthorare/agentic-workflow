using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>One skill run waiting on the owner.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The instance the launch belongs to.</param>
/// <param name="StateName">The Step's name for the owner: the recipe's display name, else the state id.</param>
/// <param name="Skill">The skill block's name ('review').</param>
/// <param name="Waiting">The last 'launch.owner-waiting' the launch recorded.</param>
/// <param name="Since">When that event was recorded.</param>
public sealed record SkillWaitingRun(LaunchId Launch, ItemId Item, InstanceAddress Instance, string StateName, string Skill, LaunchOwnerWaiting Waiting, DateTimeOffset Since);
