using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Appends every registered <see cref="IDoctorChecks"/> to the core report and applies the exit rule to the whole list. The classes run in parallel (#3390),
/// each under a time cap; a class past its cap becomes one warn row and the others carry on. Rows reach <see cref="RunDoctorRequest.OnRow"/> in registration order, each as soon as every class before it has finished.
/// </summary>
/// <param name="inner">The core doctor.</param>
/// <param name="more">The other checks, in registration order.</param>
/// <param name="log">Where each check's elapsed time goes at debug level (#3390); null logs nothing.</param>
internal sealed partial class WithMoreChecks(IService<RunDoctorRequest, RunDoctorResponse> inner, IEnumerable<IDoctorChecks> more, ILogger? log = null)
    : Decorated<RunDoctorRequest, RunDoctorResponse>(inner)
{
    private static readonly TimeSpan _defaultCap = TimeSpan.FromSeconds(20);

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

        var cap = request.Cap ?? _defaultCap;
        var checks = new List<DoctorCheck>(core.Value.Checks);
        foreach (var row in checks)
        {
            request.OnRow?.Invoke(row);
        }

        var running = more.Select(source => RunOne(source, cap, logger, cancellationToken)).ToArray();
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

    private static async Task<IReadOnlyList<DoctorCheck>> RunOne(IDoctorChecks source, TimeSpan cap, ILogger logger, CancellationToken cancellationToken)
    {
        var name = source.GetType().Name;
        var timer = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IReadOnlyList<DoctorCheck> rows;
        try
        {
            var work = Task.Run(async () => await source.Run(limit.Token).ConfigureAwait(false), CancellationToken.None);
            var finished = await Task.WhenAny(work, Task.Delay(cap, cancellationToken)).ConfigureAwait(false);
            if (finished == work)
            {
                rows = await work.ConfigureAwait(false);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                await limit.CancelAsync().ConfigureAwait(false);
                _ = work.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);   // why: a late failure of the skipped check is observed, not rethrown
                rows = [new DoctorCheck(name, CheckState.Warn, $"took over {cap.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s (skipped)", Fix: null)];
            }
        }
        finally
        {
            LogCheck(logger, name, timer.ElapsedMilliseconds);
        }

        return rows;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "doctor.check {Check} {ElapsedMs} ms")]
    private static partial void LogCheck(ILogger logger, string check, long elapsedMs);
}
