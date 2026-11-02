using System.Reflection;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Answers every call through <see cref="Inner"/>; when the hold is asked for, draft 1 has just become the current map, so the store answers SND-CONTEXT-190 and no draft is left.</summary>
public class PromotingOnHoldEngine : DispatchProxy
{
    internal RefreshEngineProxy Inner { get; set; } = null!;

    internal static IContextEngine Over(RefreshEngineProxy inner)
    {
        var proxy = Create<IContextEngine, PromotingOnHoldEngine>();
        ((PromotingOnHoldEngine)(object)proxy).Inner = inner;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IContextEngine.Hold))
        {
            Inner.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 1, CurrentHeads: []);
            Inner.HoldFailure = ContextErrors.DraftChanged.With("draft v1 is no longer the latest draft");
        }

        return targetMethod.Invoke(Inner.Proxy, args);
    }
}
