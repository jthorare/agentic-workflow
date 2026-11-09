using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>ARCH-CQRS-002 (API spec §2.5): no query handler's constructor takes a write port (ILedgerStore, ILease, IViewStore, the registry's stores).</summary>
internal static class QueryPortRules
{
    public const string Rule = "ARCH-CQRS-002";

    /// <summary>
    /// The ports a query handler may not take: the ledger and lease writers, the approvals writer, the projection writer
    /// (IViewStore), and the registry's stores, which write too (register, approve, revoke), so the registry queries read
    /// them through Core's read-only domain services instead.
    /// </summary>
    public static readonly IReadOnlyList<Type> WritePorts =
        [typeof(ILedgerStore), typeof(ILease), typeof(IViewStore), typeof(IProjectorControl), typeof(IProjectorFeed), typeof(IRecipeStore), typeof(IRecipeApprovals), typeof(IRecipeWake), typeof(IInstanceRuntime), typeof(IFactStore), typeof(ICommandNonces)];

    /// <summary>The query handlers among <paramref name="types"/> that take a write port. There is no allowlist (P1's G1 one went with C9).</summary>
    public static IReadOnlyList<string> Check(IEnumerable<Type> types) =>
        [.. Offenders(types).Select(o => $"{Rule}: {o.Handler.Name} handles a query but takes {o.Port.Name}; read through a read-only port")];

    /// <summary>Every query handler among <paramref name="types"/> with the write port it takes.</summary>
    /// <remarks>
    /// A handler's own helpers count too: a constructor parameter that is a concrete class of the handler's assembly
    /// (ContextScopes for the context queries) is followed one level, so a write port can't hide behind a helper.
    /// </remarks>
    public static IEnumerable<(Type Handler, Type Port)> Offenders(IEnumerable<Type> types) =>
        types.Where(IsQueryHandler).SelectMany(handler => Parameters(handler)
            .SelectMany(p => p.IsClass && !p.IsAbstract && p.Assembly == handler.Assembly ? [p, .. Parameters(p)] : (Type[])[p])
            .Where(WritePorts.Contains).Distinct().Select(port => (handler, port)));

    private static IEnumerable<Type> Parameters(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

    private static bool IsQueryHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IService<,>)
            && i.GenericTypeArguments[0].GetInterfaces().Any(m => m.IsGenericType && m.GetGenericTypeDefinition() == typeof(IQuery<>)));
}
