using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the sources read, with no source, and refuses every other call.</summary>
public class SourcesEngineProxy : DispatchProxy
{
    internal static IContextEngine Create() => (IContextEngine)(object)Create<IContextEngine, SourcesEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.Sources))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Result<SourcesView> none = new SourcesView([], SessionsEnabled: false, DateTimeOffset.UnixEpoch);
        return new ValueTask<Result<SourcesView>>(none);
    }
}
