namespace Aw.Sindri.Core.Setup;

/// <summary>One run, session or job that keeps a restart waiting (first-run spec decision 11).</summary>
/// <param name="Kind">'session', 'workflow-run' or 'job'.</param>
/// <param name="Ref">Its id as text: a session ref (provider/id), a run id or a job id, by kind.</param>
/// <param name="Name">A plain name, such as "Nightly gate (run 14, running)".</param>
/// <param name="Link">The deep link of its page in the app, where Stop is.</param>
public sealed record OpenRun(string Kind, string Ref, string Name, string Link);
