using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the sources view, counts the asks and refuses every other call.</summary>
public class SyncEngineProxy : DispatchProxy
{
    /// <summary>What the sources call answers: no source rows.</summary>
    public SourcesView View { get; set; } = new([], SessionsEnabled: true, DateTimeOffset.UnixEpoch);

    /// <summary>How many times the sources call was made.</summary>
    public int SourcesCalls { get; private set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static SyncEngineProxy Create() => (SyncEngineProxy)(object)Create<IContextEngine, SyncEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.Sources))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        SourcesCalls++;
        Result<SourcesView> answer = View;
        return new ValueTask<Result<SourcesView>>(answer);
    }
}
