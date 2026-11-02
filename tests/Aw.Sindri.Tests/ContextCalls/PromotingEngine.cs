using System.Reflection;
using Aw.Context.Core.Map;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Answers every call through <see cref="Inner"/>; once the review queue was read, draft 1 is the current map and no draft is left.</summary>
public class PromotingEngine : DispatchProxy
{
    internal RefreshEngineProxy Inner { get; set; } = null!;

    internal static Aw.Context.Core.Facade.IContextEngine Over(RefreshEngineProxy inner)
    {
        var proxy = Create<Aw.Context.Core.Facade.IContextEngine, PromotingEngine>();
        ((PromotingEngine)(object)proxy).Inner = inner;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var answer = targetMethod!.Invoke(Inner.Proxy, args);
        if (targetMethod.Name == nameof(Aw.Context.Core.Facade.IContextEngine.ReviewQueue))
        {
            Inner.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 1, CurrentHeads: []);
        }

        return answer;
    }
}
