using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.LensSupport;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetLenses;

/// <summary>
/// The intent, product and code bands around one node (spec §8.9): the map the caller may read (a session's graph is already cut
/// to its repo) as of the current version, the draft or a retained version, the anchor resolved like any node name, the view built by
/// <see cref="LensesBuilder"/> to the profile's cascade depth, and every label cleaned like every answer's. Only text a model wrote and nobody confirmed is fenced; file names are not.
/// </summary>
internal sealed class GetLensesService(MapReader reader, LensBasisReader bases, IScrubberFactory scrubbers, ICodeSymbolSource symbols) : IService<GetLensesRequest, LensesView>
{
    public async ValueTask<Result<LensesView>> Handle(GetLensesRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var depth = request.Depth ?? settings.Value.CascadeDepth;
        if (QueryRules.CheckDepth(depth) is { } badDepth)
        {
            return badDepth;
        }

        var hops = QueryRules.ParseHops(request.Hops);
        if (hops.IsError)
        {
            return hops.Error;
        }

        var read = await reader.ReadViewAt(request.Scope, request.Read, cancellationToken).ConfigureAwait(false);
        var anchor = read.IsError ? read.Error : AnchorMatch.Resolve(read.Value.Graph, request.Anchor);
        if (read.IsError || anchor.IsError)
        {
            return read.IsError ? read.Error : anchor.Error;
        }

        var basis = (await bases.For(read.Value, settings.Value, request.Scope, request.Read.Draft, cancellationToken).ConfigureAwait(false)) with { Cascade = QueryRules.CascadeFor(hops.Value, settings.Value) };
        var lenses = LensesBuilder.Build(basis, read.Value.Graph, anchor.Value.Key, depth, read.Value.Conflicts);
        var view = lenses.IsError ? lenses : await WithSymbols(lenses.Value, settings.Value, request.Scope, read.Value.Snapshot.Version, cancellationToken).ConfigureAwait(false);
        return view.Map(v => LensTexts.Clean(v, new MapText(built.Value, FencePolicies.For(request.Scope.Caller))));
    }

    // Phase 9: the code band's symbols for a scope of one repo (a file's repo is not on the lens row, so a wider scope stays file-level). The links come from the files' file-level 'implements'.
    private async Task<Result<LensesView>> WithSymbols(LensesView view, ContextSettings settings, ContextScope scope, MapVersion version, CancellationToken cancellationToken)
    {
        var repos = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        var code = view.Bands.Code;
        if (repos.IsError || repos.Value.Count != 1 || code.Files.Count == 0)
        {
            return view;
        }

        var links = code.Files.Where(f => f.Implements.Count > 0).Select(f => (f.Path, f.Implements)).ToList();
        var column = await SymbolColumn.Read(symbols, new SymbolColumnReading(repos.Value[0], version, [.. code.Files.Select(f => f.Path)], links), cancellationToken).ConfigureAwait(false);
        return view with { Bands = view.Bands with { Code = code with { Symbols = column } } };
    }
}
