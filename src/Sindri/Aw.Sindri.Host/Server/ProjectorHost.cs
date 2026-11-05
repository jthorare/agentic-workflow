using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Runs the projector while 'sindri serve' runs: a pass, then a wait for the ledger signal (API spec §2.3, §5.5) that ends at
/// once when an append lands past the head the pass saw, or after a second on the clock (the poll, for another process's append). A stalled projector's pass is a no-op until its 30 s retry (Projector.StallRetry).
/// </summary>
internal sealed partial class ProjectorHost(Projector projector, ILedgerSignal signal, TimeProvider clock, ILogger<ProjectorHost> log) : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pass = await projector.CatchUp(stoppingToken).ConfigureAwait(false);
                if (pass == ProjectorPass.CaughtUp)
                {
                    _ = await signal.WaitAfter(projector.SeenHead, _tick, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(_tick, clock, stoppingToken).ConfigureAwait(false);   // a stalled or failed pass has no head to wait past: poll
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken pass must not stop the read side; the next tick tries again
            {
                LogPassFailed(log, e.GetType().Name, e.Message);
                projector.Threw(e);
                await Task.Delay(_tick, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The projector's pass threw {Type}: {Detail}")]
    private static partial void LogPassFailed(ILogger logger, string type, string detail);
}
