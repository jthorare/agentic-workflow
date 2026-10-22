using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>What the code running a job can see of it (API P6): its id and kind, where to report a progress line, and how to park on a signal.</summary>
/// <param name="Job">The running job.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Progress">Takes one progress line; the runtime cleans, bounds and records it, so the caller never blocks on the ledger.</param>
/// <param name="Waiting">Parks the job on a named signal (setup-once spec §5.6), or null where the runtime has no waits (a test's inline runner).</param>
public sealed record JobContext(JobId Job, JobKind Kind, IProgress<string> Progress, IJobWaiting? Waiting = null);
