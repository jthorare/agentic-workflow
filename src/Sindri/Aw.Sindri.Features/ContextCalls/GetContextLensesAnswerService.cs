using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Picks the single or the set read by the number of distinct anchors (none or one: schema 1), and runs it.</summary>
internal sealed class GetContextLensesAnswerService(IService<GetContextLensesRequest, LensesView> single, IService<GetContextLensesSetRequest, LensesSetView> set)
    : IService<GetContextLensesAnswerRequest, LensesAnswer>
{
    public async ValueTask<Result<LensesAnswer>> Handle(GetContextLensesAnswerRequest request, CancellationToken cancellationToken)
    {
        if (QueryRules.CheckPage(request.Limit, request.Offset) is { } refused)
        {
            return refused;
        }

        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToArray();
        if (anchors.Length > 1)
        {
            var many = await set.Handle(new GetContextLensesSetRequest(anchors, request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
            return many.IsError ? many.Error : new LensesAnswer(One: null, Many: Page(many.Value, request));
        }

        if (anchors.Length == 0)
        {
            return QueryRules.CheckAnchors(anchors, "lenses")!; // why: CheckAnchors answers an error for an empty set
        }

        var one = await single.Handle(new GetContextLensesRequest(anchors[0], request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
        return one.IsError ? one.Error : new LensesAnswer(Page(one.Value, request), Many: null);
    }

    private static LensesView Page(LensesView view, GetContextLensesAnswerRequest request) =>
        request.Limit is null && request.Offset is null ? view : LensPages.Window(view, request.Limit ?? int.MaxValue, request.Offset ?? 0);   // why: a client that names no window (the UI, a script) gets everything; the 100-item default belongs to the MCP tools

    private static LensesSetView Page(LensesSetView view, GetContextLensesAnswerRequest request) =>
        request.Limit is null && request.Offset is null ? view : LensPages.Window(view, request.Limit ?? int.MaxValue, request.Offset ?? 0);   // why: a client that names no window (the UI, a script) gets everything; the 100-item default belongs to the MCP tools
}
