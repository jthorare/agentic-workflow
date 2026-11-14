using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory start route: answers from a queue (a 2xx when it is empty), records every request, and can hold an answer until the test releases it.</summary>
internal sealed class FakeNewAgentRoute : INewAgentRoute
{
    private readonly Queue<Result<AgentStartedView>> _answers = new();
    private TaskCompletionSource? _gate;

    /// <summary>Every request, in order.</summary>
    public List<StartAgentRequest> Sent { get; } = [];

    /// <summary>Answers the next call with a refusal.</summary>
    /// <param name="code">The SND-AGENT code.</param>
    /// <param name="message">The words the server sends.</param>
    /// <param name="command">The sign-in command (013), or null.</param>
    public void Refuse(string code, string message, string? command = null)
    {
        var error = new Error(code, ErrorKind.Invalid, message);
        if (command is not null)
        {
            error = error.WithExtension("command", JsonDocument.Parse(JsonSerializer.Serialize(command)).RootElement.Clone());
        }

        _answers.Enqueue(error);
    }

    /// <summary>Holds the next answer until <see cref="Release"/>.</summary>
    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the held answer go.</summary>
    public void Release() => _gate?.SetResult();

    public async Task<Result<AgentStartedView>> Start(Uri api, StartAgentRequest request, CancellationToken cancellationToken)
    {
        Sent.Add(request);
        if (_gate is { } gate)
        {
            await gate.Task.ConfigureAwait(true);
            _gate = null;
        }

        return _answers.Count > 0
            ? _answers.Dequeue()
            : new AgentStartedView("sindri-" + request.ActId, request.ActId, "starting", new AgentStartedHost("/tmp/tmux-1/sindri-launch", "sindri-" + request.ActId, "%1"));
    }
}
