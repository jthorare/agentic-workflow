using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An engine that answers the index status reads from a <see cref="FakeIndexSync"/> and the context.db store check from a set result.</summary>
public class IndexStoreEngineProxy : DispatchProxy
{
    private FakeIndexSync _index = null!;
    private Result<MapHealth> _store = default!;

    internal static IContextEngine Create(FakeIndexSync index, Result<MapHealth> store)
    {
        var proxy = Create<IContextEngine, IndexStoreEngineProxy>();
        var self = (IndexStoreEngineProxy)(object)proxy;
        self._index = index;
        self._store = store;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IContextEngine.CheckStore))
        {
            return new ValueTask<Result<MapHealth>>(_store);
        }

        try
        {
            return targetMethod!.Invoke(_index, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }
}
