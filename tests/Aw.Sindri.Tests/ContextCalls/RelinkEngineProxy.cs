using System.Reflection;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linking;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the relink and refuses every other call.</summary>
public class RelinkEngineProxy : DispatchProxy
{
    public Result<RelinkPinAliasesResult> Answer { get; set; } = new RelinkPinAliasesResult("h1", MapVersion: 3, Records: 40, Changed: 2, ElapsedMs: 150,
        [new PinLinkCount(ConceptId.From("payroll"), 4, 7), new PinLinkCount(ConceptId.From("rota"), 0, 1)]);

    public int Relinks { get; private set; }

    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static RelinkEngineProxy Create() => (RelinkEngineProxy)(object)Create<IContextEngine, RelinkEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.RelinkPinAliases))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Relinks++;
        return new ValueTask<Result<RelinkPinAliasesResult>>(Answer);
    }
}
