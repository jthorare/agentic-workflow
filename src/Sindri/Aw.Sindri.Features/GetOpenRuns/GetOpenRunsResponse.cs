namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>The open set.</summary>
/// <param name="Runs">Each open run, session or job; empty when a restart would not wait.</param>
internal sealed record GetOpenRunsResponse(IReadOnlyList<OpenRun> Runs);

/// <summary>One open run, session or job.</summary>
/// <param name="Kind">session, workflow-run or job.</param>
/// <param name="Id">Its id.</param>
/// <param name="Name">A plain name, such as "Nightly gate (run 14, an agent step)".</param>
/// <param name="Link">The path of its page in the app.</param>
internal sealed record OpenRun(string Kind, string Id, string Name, string Link);
