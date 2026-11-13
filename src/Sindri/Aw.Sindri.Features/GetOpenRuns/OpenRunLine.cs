namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>One open run, session or job.</summary>
/// <param name="Kind">session, workflow-run or job.</param>
/// <param name="Id">Its id.</param>
/// <param name="Name">A plain name, such as "Nightly gate (run 14, an agent step)".</param>
/// <param name="Link">The path of its page in the app.</param>
internal sealed record OpenRunLine(string Kind, string Id, string Name, string Link);
