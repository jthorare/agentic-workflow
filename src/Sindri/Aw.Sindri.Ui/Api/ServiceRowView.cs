namespace Aw.Sindri.Ui.Api;

/// <summary>A scheduled service in GET /sindri/services.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence, as the profile writes it (5s, 30m).</param>
/// <param name="Paused">True while paused.</param>
/// <param name="LastRun">Its latest run, or null when it has not run.</param>
/// <param name="NextSlot">When it runs next, or null when none is due.</param>
/// <param name="Job">The job of a manual run in flight, or null.</param>
/// <param name="Actions">The actions the service allows (run, pause, resume); null when the answer does not say, which shows them all.</param>
/// <param name="OverdueSince">The slot the scheduler missed, or null when the service is on time (the member is then left out).</param>
/// <param name="Description">What the service does, in the server's words, or null when it sends none.</param>
/// <param name="PausedBy">Who paused it (human:..., session:..., service:...), or null; the app turns it into words and never shows it raw.</param>
/// <param name="PausedAt">When it was paused, or null.</param>
internal sealed record ServiceRowView(string Name, string Every, bool Paused, ServiceRunView? LastRun = null, DateTimeOffset? NextSlot = null, string? Job = null, IReadOnlyList<string>? Actions = null,
    DateTimeOffset? OverdueSince = null, string? Description = null, string? PausedBy = null, DateTimeOffset? PausedAt = null);
