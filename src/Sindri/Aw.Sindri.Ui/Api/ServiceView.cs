namespace Aw.Sindri.Ui.Api;

/// <summary>A scheduled service in the status.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence, as the profile writes it (5s, 30m).</param>
/// <param name="LastRun">When it last ran, if ever.</param>
/// <param name="Actions">What the server would allow now (run, pause, resume), or null from a server that doesn't say.</param>
internal sealed record ServiceView(string Name, string Every, DateTimeOffset? LastRun, IReadOnlyList<string>? Actions = null);
