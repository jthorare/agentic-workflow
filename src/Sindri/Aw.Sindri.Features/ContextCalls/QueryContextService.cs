using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Answers a context question through the engine, in the current caller's scope.</summary>
internal sealed class QueryContextService(ContextScopes scopes, IContextEngine engine, IItemResolver items, ICallerContextAccessor callers) : IService<QueryContextRequest, ContextResult>
{
    public async ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken)
    {
        var depth = QueryRules.ParseDepth(request.Depth);
        var window = Window(request.Limit, request.Offset);
        var diff = request.Diff is null ? (Result<DiffRange?>)(DiffRange?)null : DiffRange.Parse(request.Diff).Map<DiffRange?>(d => d);
        var repo = request.Repo is null ? (Result<ContextRepoName?>)(ContextRepoName?)null : Repo(request.Repo);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var item = await ItemKeys.Resolve(request.Item, items, callers.Current, cancellationToken).ConfigureAwait(false);
        return item.IsError ? item.Error
            : diff.IsError ? diff.Error
            : repo.IsError ? repo.Error
            : depth.IsError ? depth.Error
            : window.IsError ? window.Error
            : scope.IsError ? scope.Error
            : await engine.Query(new ContextQuery(request.Q, request.Node, item.Value, request.Symbol, depth.Value, Budget: null, request.Draft, diff.Value, repo.Value) { MapVersion = request.MapVersion, Hops = request.Hops, Concept = request.Concept, Window = window.Value },
                scope.Value, cancellationToken).ConfigureAwait(false);
    }

    // The page of ranked records to read (#3370): neither = every match (#938); an offset alone reads from there to the end.
    private static Result<QueryWindow?> Window(string? limit, string? offset)
    {
        if (limit is null && offset is null)
        {
            return (QueryWindow?)null;
        }

        var take = int.MaxValue;
        var skip = 0;
        return (limit is null || (int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out take) && take >= 1))
            && (offset is null || int.TryParse(offset, NumberStyles.None, CultureInfo.InvariantCulture, out skip))
            ? new QueryWindow(skip, take)
            : ContextErrors.QueryInvalid.With("limit is a whole number of 1 or more and offset a whole number of 0 or more");
    }

    private static Result<ContextRepoName?> Repo(string name) =>
        ContextRepoName.TryFrom(name, out var repo)
            ? (ContextRepoName?)repo
            : ContextErrors.QueryInvalid.With("a repo names a profile repo: 1 to 100 letters, digits, '.', '_' or '-'");
}
