using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The takeover command of the fake (<see cref="FakeSindriApi"/>, continued): every command received, and the answer it gives (spec-shaped).</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Instance, ApplyCommandBody Body)> _commands = [];

    /// <summary>POST .../commands' answer.</summary>
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
        return Answer<ApplyCommandAccepted>(api, "/sindri/workflow/instances/" + Uri.EscapeDataString(instanceId) + "/commands", CommandAnswer);
    }
}
