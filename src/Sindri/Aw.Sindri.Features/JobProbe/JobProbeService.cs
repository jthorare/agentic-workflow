using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.JobProbe;

/// <summary>
/// The job-probe diagnostic service (API P6 unit I): a long, noisy job for the prover and the owner's hand checks. It is not a built-in, so it runs only
/// when a profile names it ('services: job-probe: {every: 24h}') and, as a manual run, when 'sindri services run job-probe' starts it. It reports
/// <see cref="Lines"/> progress lines, one every <see cref="Pause"/> (about 62 s), and honours the token between lines. Line 5 holds an OSC-8
/// hyperlink, line 6 a credential-shaped fake the scrubber catches, line 7 is 300 characters: the runtime must escape, scrub and cut them, and keep only the last 200
/// lines. A scheduled run has no job context and reports nothing.
/// </summary>
/// <param name="jobs">The running job, whose progress takes the lines.</param>
/// <param name="clock">Times the pauses.</param>
internal sealed class JobProbeService(IJobContextAccessor jobs, TimeProvider clock) : IScheduledService
{
    public const int Lines = 250;

    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    public static readonly ServiceName ServiceId = ServiceName.From(ServiceDescriptions.JobProbeName);

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = ServiceDescriptions.JobProbe;

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        for (var n = 1; n <= Lines; n++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jobs.Current?.Progress.Report(Line(n));
            if (n < Lines)
            {
                await Task.Delay(Pause, clock, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"ok: {Lines} lines"));
    }

    private static string Line(int n) => n switch
    {
        5 => "link \u001b]8;;https://example.invalid/probe\u001b\\probe\u001b]8;;\u001b\\ end",
        6 => "token " + "ghp_" + new string('a', 36),   // built at run time; a credential-shaped fake the scrubber catches
        7 => new string('x', 300),
        _ => string.Create(CultureInfo.InvariantCulture, $"probe line {n} of {Lines}"),
    };
}
