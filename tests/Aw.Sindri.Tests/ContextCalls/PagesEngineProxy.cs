using System.Reflection;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers the pages read and the doctor rows beside it, remembers what it was asked and refuses every other call.</summary>
public class PagesEngineProxy : DispatchProxy
{
    /// <summary>What the pages read answers.</summary>
    public Result<PagesView> Answer { get; set; } = new PagesView([], Total: 0, Truncated: false, "v1");

    /// <summary>When set, answers each pages query itself (a slice by offset, say) in place of <see cref="Answer"/>.</summary>
    public Func<PagesQuery, Result<PagesView>>? AnswerFor { get; set; }

    /// <summary>The pages queries asked, in order.</summary>
    public IList<PagesQuery> Queries { get; } = [];

    /// <summary>The scopes the pages and pages-check calls carried, in order.</summary>
    public IList<ContextScope?> Scopes { get; } = [];

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static PagesEngineProxy Create() => (PagesEngineProxy)(object)Create<IContextEngine, PagesEngineProxy>();

    /// <summary>One owner page 'schedule-week' with one action that expresses one concept.</summary>
    internal static PagesView OneOwnerPage() => new(
        [new PageRow(PageId.From("schedule-week"), NodeKey.From("surface:route/GET:/schedule/week"), "Schedule week", "app", "owner", Code: true,
            [new PageActionRow(PageActionId.From("submit-schedule"), NodeKey.From("action:WeekPage.submit"), "Submit schedule", "owner", Code: false, ["scheduling"])])],
        Total: 1, Truncated: false, "v13");

    private Result<PagesView> Pages(PagesQuery query, ContextScope scope)
    {
        Queries.Add(query);
        Scopes.Add(scope);
        return AnswerFor?.Invoke(query) ?? Answer;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IContextEngine.Pages):
                return new ValueTask<Result<PagesView>>(Pages((PagesQuery)args![0]!, (ContextScope)args[1]!));
            case nameof(IContextEngine.CheckPages):
                Scopes.Add((ContextScope?)args![0]);
                return new ValueTask<Result<MapHealth>>(new MapHealth("context:pages", MapHealthState.Ok, "no pages named", string.Empty));
            case nameof(IContextEngine.CheckMap):
                return new ValueTask<Result<MapHealth>>(new MapHealth("context:map", MapHealthState.Ok, "ok", string.Empty));
            case nameof(IContextEngine.CheckRetention):
                return new ValueTask<Result<MapHealth>>(new MapHealth("context:retention", MapHealthState.Ok, "ok", string.Empty));
            case nameof(IContextEngine.MapStatus):
                return new ValueTask<Result<MapStatusView>>(Context.Core.ContextErrors.NoMap.With("no map yet"));
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
