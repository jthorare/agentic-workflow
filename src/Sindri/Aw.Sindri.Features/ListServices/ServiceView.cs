using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>One service in the list.</summary>
/// <param name="Name">The service.</param>
/// <param name="Every">Its cadence, as the profile writes it.</param>
/// <param name="Paused">True when paused (P6).</param>
/// <param name="LastRun">Its last run, or null before the first.</param>
/// <param name="NextSlot">The last scheduled slot plus the cadence, or null before the first scheduled run and while paused.</param>
/// <param name="Job">The running job of this service (a manual run), or null: the app's way back into the job panel.</param>
/// <param name="Actions">What the server would allow now, from run, pause and resume (<see cref="ServiceActions"/>): the app hides the rest.</param>
/// <param name="Description">One sentence on what the service does, or null when the build has none (the member is then left out).</param>
/// <param name="PausedBy">Who paused it, or null (left out while running).</param>
/// <param name="PausedAt">When it was paused, or null (left out while running). A pause records no reason, so there is no reason member.</param>
/// <param name="OverdueSince">The slot the scheduler really missed: its own next slot, when that is more than a minute past and the service is not paused; null (left out) otherwise.</param>
public sealed record ServiceView(ServiceName Name, string Every, bool Paused, LastRun? LastRun, DateTimeOffset? NextSlot, JobId? Job, IReadOnlyList<string> Actions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PausedBy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? PausedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? OverdueSince = null);
