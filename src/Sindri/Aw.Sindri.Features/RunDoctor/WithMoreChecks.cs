using System.Diagnostics;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Appends every registered <see cref="IDoctorChecks"/>, then the request's harness probe, to the core report and applies the exit rule to the whole list.
/// The classes and the probe run in parallel (#3390), with no time cap: rows stream, so a slow check shows as the one still open. A class that throws
/// becomes one fail row and the others carry on. Rows reach <see cref="RunDoctorRequest.OnRow"/> in registration order, each as soon as every class before it has finished.
/// </summary>
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
        foreach (var row in checks)
        {
            request.OnRow?.Invoke(row);
        }

        var sources = more.Select(static source => (source.GetType().Name, Run: (Func<CancellationToken, ValueTask<IReadOnlyList<DoctorCheck>>>)source.Run)).ToList();
        if (request.Harnesses is { } harnesses)
        {
            sources.Add(("harnesses", harnesses));
        }

        var running = sources.Select(source => RunOne(source.Name, source.Run, logger, cancellationToken)).ToArray();
        foreach (var task in running)   // why: awaited in registration order, so a row prints once every class before it is done and the output order is stable
        {
            var rows = await task.ConfigureAwait(false);
            checks.AddRange(rows);
            foreach (var row in rows)
            {
                request.OnRow?.Invoke(row);
            }
        }

        return new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
    }

    private static async Task<IReadOnlyList<DoctorCheck>> RunOne(string name, Func<CancellationToken, ValueTask<IReadOnlyList<DoctorCheck>>> run, ILogger logger, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return await Task.Run(async () => await run(cancellationToken).ConfigureAwait(false), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)   // boundary: one broken class must not end the pass while the others still run against the scope (#3390)
        {
            return [new DoctorCheck(name, CheckState.Fail, $"the check threw {ex.GetType().Name}: {ex.Message}", "AW_LOG_LEVEL=debug sindri doctor")];
        }
        finally
        {
            LogCheck(logger, name, timer.ElapsedMilliseconds);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "doctor.check {Check} {ElapsedMs} ms")]
    private static partial void LogCheck(ILogger logger, string check, long elapsedMs);
}
