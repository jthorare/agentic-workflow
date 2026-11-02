using System.Reflection;
using Aw.Context.Core.Map;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Answers every call through <see cref="Inner"/>; once the review queue was read, draft 1 is replaced by a newer draft 2 and nothing is current.</summary>
public class ReplacingEngine : DispatchProxy
{
    internal RefreshEngineProxy Inner { get; set; } = null!;

    internal static Aw.Context.Core.Facade.IContextEngine Over(RefreshEngineProxy inner)
    {
        var proxy = Create<Aw.Context.Core.Facade.IContextEngine, ReplacingEngine>();
        ((ReplacingEngine)(object)proxy).Inner = inner;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var answer = targetMethod!.Invoke(Inner.Proxy, args);
        if (targetMethod.Name == nameof(Aw.Context.Core.Facade.IContextEngine.ReviewQueue))
        {
            Inner.State = new MapPublishState(Hold: null, LatestDraft: 2, Current: null, CurrentHeads: []);
        }

        return answer;
    }
}
