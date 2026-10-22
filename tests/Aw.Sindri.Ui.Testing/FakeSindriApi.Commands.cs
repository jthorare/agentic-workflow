using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The desk report of the fake (<see cref="FakeSindriApi"/>, continued): every report received, and the answer it gives (spec-shaped; no API golden covers it yet).</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Instance, CompleteStepBody Body)> _completed = [];

    /// <summary>POST .../runs/{run}/complete's answer.</summary>
    public RouteAnswer CompleteAnswer { get; set; } = RouteAnswer.SpecShaped("workflow-complete", "accepted");

    /// <summary>Held open until set: a report received while it was set waits for it (the app is sending meanwhile).</summary>
    public TaskCompletionSource? CompleteGate { get; set; }

    /// <summary>Every report received, in order: the instance of the route and the body.</summary>
    public IReadOnlyList<(string Instance, CompleteStepBody Body)> CompleteCalls { get { lock (_gate) { return [.. _completed]; } } }

    public async Task<Result<CompleteStepAccepted>> CompleteStep(Uri api, string instanceId, CompleteStepBody body, CancellationToken cancellationToken)
    {
        if (NotReceived<CompleteStepAccepted>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _completed.Add((instanceId, body));
        }

        await WaitFor(CompleteGate, cancellationToken).ConfigureAwait(false);
        return Answer<CompleteStepAccepted>(api, WorkflowRoutes.Complete(instanceId, body.Run), CompleteAnswer);
    }

    private readonly List<(string Instance, ApplyCommandBody Body)> _commands = [];

    /// <summary>POST .../commands' answer (the accepted shape is the same as the desk report's).</summary>
    public RouteAnswer CommandAnswer { get; set; } = RouteAnswer.SpecShaped("workflow-complete", "accepted");

    /// <summary>Held open until set: a command received while it was set waits for it.</summary>
    public TaskCompletionSource? WorkflowCommandGate { get; set; }

    /// <summary>Every command received, in order: the instance of the route and the body.</summary>
    public IReadOnlyList<(string Instance, ApplyCommandBody Body)> CommandCalls { get { lock (_gate) { return [.. _commands]; } } }

    public async Task<Result<ApplyCommandAccepted>> ApplyCommand(Uri api, string instanceId, ApplyCommandBody body, CancellationToken cancellationToken)
    {
        if (NotReceived<ApplyCommandAccepted>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _commands.Add((instanceId, body));
        }

        await WaitFor(WorkflowCommandGate, cancellationToken).ConfigureAwait(false);
        return Answer<ApplyCommandAccepted>(api, WorkflowRoutes.Commands(instanceId), CommandAnswer);
    }
}
