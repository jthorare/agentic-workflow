using System.Reflection;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the mapped repos' heads and the map's publish state, counts the asks and refuses every other call.</summary>
public class HeadsEngineProxy : DispatchProxy
{
    /// <summary>What the heads call answers.</summary>
    public IReadOnlyList<RepoHead> Heads { get; set; } = [];

    /// <summary>What the publish-state call answers: no hold, no draft, no current version.</summary>
    public MapPublishState State { get; set; } = new(Hold: null, LatestDraft: null, Current: null, CurrentHeads: []);

    /// <summary>When set, the heads call fails with it.</summary>
    public Error? Failure { get; set; }

    /// <summary>How many times it was asked.</summary>
    public int Calls { get; private set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static HeadsEngineProxy Create() => (HeadsEngineProxy)(object)Create<IContextEngine, HeadsEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IContextEngine.Held))
        {
            return new ValueTask<Result<MapPublishState>>(State);
        }

        if (targetMethod?.Name != nameof(IContextEngine.MapHeads))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Calls++;
        var answer = Results.Success(Heads);
        if (Failure is { } error)
        {
            answer = error;
        }

        return new ValueTask<Result<IReadOnlyList<RepoHead>>>(answer);
    }
}
