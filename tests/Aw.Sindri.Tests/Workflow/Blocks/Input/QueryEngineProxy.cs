using System.Reflection;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>An engine that answers only Query, from a script, and records each ask.</summary>
public class QueryEngineProxy : DispatchProxy
{
    public IList<(ContextQuery Query, ContextScope Scope)> Calls { get; } = [];

    public Func<Result<ContextResult>> Answer { get; set; } = () => ContextRetrieveInputTests.Result();

    internal IContextEngine Engine => (IContextEngine)(object)this;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.Query) || args is not [ContextQuery query, ContextScope scope, ..])
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Calls.Add((query, scope));
        return new ValueTask<Result<ContextResult>>(Answer());
    }
}
