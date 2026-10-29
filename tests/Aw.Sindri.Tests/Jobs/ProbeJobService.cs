using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The scripted job's work.</summary>
/// <param name="jobs">The running job's context.</param>
/// <param name="latch">What the 'stubborn' mode waits on.</param>
internal sealed class ProbeJobService(IJobContextAccessor jobs, Commands.Chain.FixtureLatch latch) : IService<ProbeJobRequest, ProbeReport>
{
    public async ValueTask<Result<ProbeReport>> Handle(ProbeJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Mode)
        {
            case "job":
                await Task.Yield();
                return new ProbeReport(jobs.Current?.Job.Value ?? "none");
            case "stubborn":   // ignores its token: it returns only when the test releases the latch
                latch.Start();
                await latch.Release.Task.ConfigureAwait(false);
                _ = latch.Returned.TrySetResult();
                return new ProbeReport("late");
            case "own-cancel":   // like 'stubborn', then throws a cancellation of its own that is not its token's (a timeout)
                latch.Start();
                await latch.Release.Task.ConfigureAwait(false);
                _ = latch.Returned.TrySetResult();
                throw new OperationCanceledException("a timeout of its own");
            case "fail" or "throw" or "big" or "secret" or "repo" or "esc" or "keyed" or "sql":
                return Immediate(request.Mode);
            default:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return new ProbeReport("unreachable");
        }
    }

    private Result<ProbeReport> Immediate(string mode)
    {
        switch (mode)
        {
            case "fail":
                return SindriErrors.ServiceFailed.With("it broke");
            case "sql":   // a SQLite error as SqliteErrors builds it: the statement first, the reason last
                return SindriErrors.ServiceFailed.With("INSERT INTO items (" + new string('c', 400) + ") VALUES (@p): SQLite Error 19: 'FOREIGN KEY constraint failed'.");
            case "throw":
                throw new InvalidOperationException("kaboom");
            case "esc":
                return new ProbeReport("\u001b]0;title\u0007red\u001b[31m");
            case "keyed":   // a secret in a result string and in a property-like text
                return new ProbeReport("see token ghp_" + new string('b', 36));
            case "big":
                return new ProbeReport(new string('x', 20_000));
            default:
                jobs.Current?.Progress.Report("\u001b]8;;x\u0007 token ghp_" + new string('a', 36) + " " + new string('x', 400));   // an OSC, a credential and 400 characters: all three rules apply to one line
                return new ProbeReport("ok");
        }
    }
}
