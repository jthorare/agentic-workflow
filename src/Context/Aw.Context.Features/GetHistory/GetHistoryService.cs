using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetHistory;

/// <summary>
/// The history lens (scoping spec C5, C10): the map the caller may read as of the current version, the draft or a retained version, the anchors resolved like the lenses' set
/// (an anchor the version doesn't hold, a session can't see or a deny glob hides is listed in 'missingAnchors'), the view built by <see cref="HistoryPipeline"/>, and every
/// piece of text cleaned like every answer's: scrubbed, one line, fenced when the caller's policy fences untrusted text.
/// </summary>
internal sealed class GetHistoryService(MapReader reader, HistoryPipeline pipeline, IScrubberFactory scrubbers) : IService<GetHistoryRequest, HistoryView>
{
    public async ValueTask<Result<HistoryView>> Handle(GetHistoryRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToList();
        if (QueryRules.CheckAnchors(anchors, "history") is { } refused)
        {
            return refused;
        }

        var read = await reader.ReadViewAt(request.Scope, request.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        using var band = FillBands.Start(FillBands.History);
        var view = await pipeline.Run(new HistoryInputs(read.Value, request.Window, request.Read.Draft), anchors, settings.Value, request.Scope, cancellationToken).ConfigureAwait(false);
        return view.Map(v => HistoryTexts.Clean(v, new MapText(built.Value, FencePolicies.For(request.Scope.Caller))));
    }
}
