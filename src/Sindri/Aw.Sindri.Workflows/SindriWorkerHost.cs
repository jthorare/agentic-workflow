using System.Globalization;
using Aw.Sindri.Core.Control;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="ITemporalWorker"/> over the SDK's TemporalWorker (engine spec rev 8 §0 "Operations"): the worker 'sindri
/// serve' runs on the settings' namespace and task queue of the dev server it keeps. Engine P4a: a frontend that doesn't answer is
/// logged and serve runs on without a worker (P4 makes it a health failure).
/// </summary>
/// <param name="settings">The namespace and the task queue.</param>
/// <param name="steps">The RunStep activities.</param>
/// <param name="events">The RecordEvent activity.</param>
/// <param name="items">The ReadItemRepo activity.</param>
/// <param name="effects">The effect activities (engine P6).</param>
/// <param name="logger">The log.</param>
internal sealed partial class SindriWorkerHost(
    TemporalSettings settings, RunStepActivities steps, RecordEventActivities events, FactReadActivities items, EffectActivities effects, ILogger<SindriWorkerHost> logger) : ITemporalWorker
{
    public async Task Run(TemporalEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var target = string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{endpoint.Port}");
        ITemporalClient client;
        try
        {
            client = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(target) { Namespace = settings.Namespace }).ConfigureAwait(false);
        }
        catch (Exception e) // boundary: serve runs on; the doctor's temporal line shows why (the connect takes no token)
        {
            NoWorker(logger, target, e.Message);
            return;
        }

        try
        {
            using var worker = new TemporalWorker(client, SindriWorker.Options(settings, steps, events, items, effects));
            Polling(logger, target, settings.TaskQueue);
            await worker.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // serve is stopping
        }
        catch (Exception e) // boundary: the port promises Run never throws; the worker logs why it stopped and serve stops cleanly
        {
            Stopped(logger, target, e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "worker polls task queue {Queue} on {Target}")]
    private static partial void Polling(ILogger logger, string target, string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "the worker on {Target} stopped: {Problem}")]
    private static partial void Stopped(ILogger logger, string target, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "no worker: Temporal at {Target} doesn't answer: {Problem}")]
    private static partial void NoWorker(ILogger logger, string target, string problem);
}
