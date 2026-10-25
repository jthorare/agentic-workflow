namespace Aw.Sindri.Ui.Api;

/// <summary>A scheduled service in GET /sindri/services.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence, as the profile writes it (5s, 30m).</param>
/// <param name="Paused">True while paused.</param>
/// <param name="LastRun">Its latest run, or null when it has not run.</param>
/// <param name="NextSlot">When it runs next, or null when none is due.</param>
/// <param name="Job">The job of a manual run in flight, or null.</param>
/// <param name="Description">What the service does, as the API serves it; null when it does not say.</param>
/// <param name="PausedBy">Who paused it; null when the API does not say.</param>
/// <param name="PausedAt">When it was paused; null when the API does not say.</param>
/// <param name="Actions">The actions the service allows (run, pause, resume); null when the answer does not say, which shows them all.</param>
internal sealed record ServiceRowView(string Name, string Every, bool Paused, ServiceRunView? LastRun = null, DateTimeOffset? NextSlot = null, string? Job = null, IReadOnlyList<string>? Actions = null, string? Description = null, string? PausedBy = null, DateTimeOffset? PausedAt = null);
