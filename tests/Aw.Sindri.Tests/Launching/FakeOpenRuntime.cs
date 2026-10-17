using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The runtime port as far as the launcher uses it: 'open-request' takes an instance over, or is held open, or is refused.</summary>
internal sealed class FakeOpenRuntime : IInstanceRuntime
{
    private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
    private readonly List<(InstanceAddress Address, LaunchId Launch)> _requests = [];
    private readonly HashSet<string> _takenOver = new(StringComparer.Ordinal);

    public IReadOnlyList<(InstanceAddress Address, LaunchId Launch)> Requests => _requests;

    /// <summary>When set, 'OpenRequest' waits for it before answering.</summary>
    public Task? Hold { get; set; }

    /// <summary>When set, 'OpenRequest' is refused with it.</summary>
    public Error? Refuse { get; set; }

    public bool IsTakenOver(string address) => _takenOver.Contains(address);

    public async ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken)
    {
        _requests.Add((address, launch));
        if (Hold is { } hold)
        {
            await hold.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Refuse is { } refused)
        {
            return refused;
        }

        _ = _takenOver.Add(address.Value);
        var view = new InstanceView(address, _recipe, "taken-over", Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
        { Id = WorkflowInstanceId.Of(address), Version = 1 };
        return new CommandResult(view, "taken over");
    }

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException("the launcher doesn't read instances");

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("the launcher doesn't start instances");

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the launcher sends only the open-request");

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the launcher doesn't signal");
}
