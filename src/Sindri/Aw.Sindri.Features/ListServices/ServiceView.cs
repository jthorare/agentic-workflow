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
public sealed record ServiceView(ServiceName Name, string Every, bool Paused, LastRun? LastRun, DateTimeOffset? NextSlot, JobId? Job, IReadOnlyList<string> Actions);
