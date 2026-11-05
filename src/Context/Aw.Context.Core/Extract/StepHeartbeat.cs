using System.Globalization;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Runs one silent step and, while it runs, tells the progress it is alive every <see cref="Every"/> ("graphify running, 10 min"), so the job's stall watch (SND-JOB-004) and a build
/// waiting behind this one (the slot wait follows the holder's last line) never read a healthy step as a dead one. Only a step that bounds itself gets this: graphify with a
/// 'timeoutMinutes' above 0 ends on its own, so a beat can't hide a hang. With no limit (0) the caller does not wrap the step, and a graphify that hangs is exactly what the stall watch is for.
/// </summary>
public static class StepHeartbeat
{
    /// <summary>How often a running step says so.</summary>
    public static TimeSpan Every { get; } = TimeSpan.FromMinutes(5);

    /// <summary>Runs <paramref name="work"/>, reporting "&lt;step&gt; running, &lt;N&gt; min" every <see cref="Every"/> until it ends (and nothing after); null progress runs it bare.</summary>
    public static async ValueTask<T> Run<T>(string step, IProgress<string>? progress, TimeProvider clock, Func<ValueTask<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (progress is null)
        {
            return await work().ConfigureAwait(false);
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var beats = Beat(step, progress, clock, stop.Token);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await beats.ConfigureAwait(false);
        }
    }

    private static async Task Beat(string step, IProgress<string> progress, TimeProvider clock, CancellationToken stopped)
    {
        try
        {
            for (var beat = 1; ; beat++)
            {
                await Task.Delay(Every, clock, stopped).ConfigureAwait(false);
                progress.Report(string.Create(CultureInfo.InvariantCulture, $"{step} running, {(int)(Every.TotalMinutes * beat)} min"));
            }
        }
        catch (OperationCanceledException)
        {
            // why: the step ended (or its caller cancelled); the beat has nothing left to say
        }
    }
}
