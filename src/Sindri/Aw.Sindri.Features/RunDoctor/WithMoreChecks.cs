using Aw.Kernel;
using Aw.Kernel.Services;
using System.Diagnostics;
using Aw.Sindri.Core.Doctor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Appends every registered <see cref="IDoctorChecks"/> to the core report and applies the exit rule to the whole list.</summary>
/// <param name="inner">The core doctor.</param>
/// <param name="more">The other checks, in registration order.</param>
/// <param name="log">Where each check's elapsed time goes at debug level (#3390); null logs nothing.</param>
internal sealed partial class WithMoreChecks(IService<RunDoctorRequest, RunDoctorResponse> inner, IEnumerable<IDoctorChecks> more, ILogger? log = null)
    : Decorated<RunDoctorRequest, RunDoctorResponse>(inner)
{
    public override async ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken)
    {
        var logger = log ?? NullLogger.Instance;
        var timer = Stopwatch.StartNew();
        var core = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        LogCheck(logger, "core", timer.ElapsedMilliseconds);
        if (core.IsError)
        {
            return core;
        }

        var checks = new List<DoctorCheck>(core.Value.Checks);
        foreach (var source in more)
        {
            timer.Restart();
            checks.AddRange(await source.Run(cancellationToken).ConfigureAwait(false));
            LogCheck(logger, source.GetType().Name, timer.ElapsedMilliseconds);
        }

        return new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "doctor.check {Check} {ElapsedMs} ms")]
    private static partial void LogCheck(ILogger logger, string check, long elapsedMs);
}
