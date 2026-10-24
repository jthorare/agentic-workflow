using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Syncs the live intent sources (spec §8.8, S-5B): each is walked against the current map by <see cref="IntentSyncRun"/>. A source that is down is a row
/// with its code, not a failure of the call; a second sync of a source that is already running is SND-CONTEXT-056, and a source that isn't a live intent source
/// of this profile SND-CONTEXT-058. It runs as the engine itself (spec §3): a session's reading rules don't apply, so the links it writes are the same whoever
/// asks. The repo docs are refreshed by the facade next to it (ContextSources).
/// </summary>
internal sealed class SyncSourcesService(ISourceCatalog catalog, IScrubberFactory scrubberFactory, MapReader graphs, IIntentStore intents, IModelPort model, SyncLocks locks, TimeProvider clock)
    : IService<SyncSourcesRequest, SyncReport>
{
    /// <summary>The longest one run may take before it stops and saves its cursor (spec §8.8).</summary>
    internal static readonly TimeSpan RunCap = TimeSpan.FromMinutes(10);

    public async ValueTask<Result<SyncReport>> Handle(SyncSourcesRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        var wanted = repos.Bind(r => Wanted(request.Source, catalog.For(settings.Value, r, scrubbers.Value)));
        if (wanted.IsError)
        {
            return wanted.Error;
        }

        if (wanted.Value.Count == 0)
        {
            return new SyncReport([]);
        }

        var map = await graphs.ReadWithoutIntent(request.Scope, draft: false, cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        var rows = new List<SourceSync>();
        foreach (var source in wanted.Value)
        {
            var row = await Run(source, map.Value, (settings.Value, scrubbers.Value), cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return new SyncReport(rows);
    }

    private static Result<IReadOnlyList<IContextSource>> Wanted(SourceId? named, IReadOnlyList<IContextSource> all)
    {
        var live = all.Where(s => s.Describe() is { Kind: SourceKind.Intent, Freshness: SourceFreshness.Live }).OrderBy(s => s.Describe().Id.Value, StringComparer.Ordinal).ToList();
        IReadOnlyList<IContextSource> wanted = [.. live.Where(s => named is null || s.Describe().Id == named)];
        return named is { } name && wanted.Count == 0
            ? ContextErrors.SyncSourceUnknown.With($"'{name.Value}' isn't a source this profile syncs ({string.Join(", ", live.Select(s => s.Describe().Id.Value))}); fix: sindri context sources status")
            : Results.Success(wanted);
    }

    private async ValueTask<Result<SourceSync>> Run(IContextSource source, VisibleMap map, (ContextSettings Settings, Scrubbers Scrubbers) context, CancellationToken cancellationToken)
    {
        var id = source.Describe().Id;
        if (!locks.TryEnter(id.Value))
        {
            return ContextErrors.SyncRunning.With($"{id.Value} is being synced already (fix: wait for it; sindri context sources status)");
        }

        try
        {
            return await new IntentSyncRun(source, map, context, (intents, model, clock)).Execute(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            locks.Leave(id.Value);
        }
    }
}
