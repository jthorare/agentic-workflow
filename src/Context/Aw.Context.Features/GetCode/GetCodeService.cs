using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Core.Walks;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetCode;

/// <summary>
/// The code lens around one or more anchors (S-CL, scoping station C3): one snapshot of the map the caller may read (a session's graph is already cut to its repo) serves every anchor, the graph half is
/// built by <see cref="CodeLensBuilder"/>, the lexical half by <see cref="CodeLexicalSearch"/> at the map's commit, the callers and callees by <see cref="CodeSymbolSearch"/> from the code index, and every piece of map text is cleaned like every answer's. An anchor the version doesn't hold, a
/// session can't see or a deny glob hides is listed in 'missingAnchors' with one answer for every reason; none resolving is SND-CONTEXT-003 and an ambiguous name 037.
/// </summary>
internal sealed class GetCodeService(MapReader reader, LensBasisReader bases, CodeLexicalSearch lexical, CodeSymbolSearch symbols, IScrubberFactory scrubbers, WalkOptions? walk = null) : IService<GetCodeRequest, CodeView>
{
    public async ValueTask<Result<CodeView>> Handle(GetCodeRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToList();
        var mapRead = MapRead.Of(request.Draft, request.MapVersion);
        if (QueryRules.CheckAnchors(anchors, "code") is { } refused)
        {
            return refused;
        }

        if (mapRead.IsError)
        {
            return mapRead.Error;
        }

        var read = await reader.ReadViewAt(request.Scope, mapRead.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var basis = await bases.For(read.Value, settings.Value, request.Scope, request.Draft, cancellationToken).ConfigureAwait(false);
        var graphHalf = CodeLensBuilder.Build(basis, read.Value.Graph, anchors, walk ?? WalkOptions.Serial);
        if (graphHalf.IsError)
        {
            return graphHalf.Error;
        }

        var text = new MapText(built.Value, FencePolicies.For(request.Scope.Caller));
        var found = await lexical.Find(new CodeLexicalRequest(graphHalf.Value, read.Value, settings.Value, request.Scope, name => Term(text, name)), cancellationToken).ConfigureAwait(false);
        var merged = CodeLensMerge.With(graphHalf.Value, found);
        var indexed = await symbols.Find(merged, read.Value, settings.Value, request.Scope, cancellationToken).ConfigureAwait(false);
        return CodeTexts.Clean(CodeLensSymbols.With(merged, indexed), text);
    }

    // A name the answer could not carry (a secret, a blocked word) gives no search term.
    private static string Term(MapText text, string name) => text.Line(name) is var line && line.Contains("[REDACTED", StringComparison.Ordinal) ? string.Empty : line;
}
