using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// POST /sindri/index/sync: pulls one record source into the index now, as its own job (owned-index spec §5.5; #1597). Only that source's feed ticks; the other feeds are
/// left alone and the scheduled 'index-sync' service running does not refuse it. A sync of the same source already running is that row's SND-CONTEXT-056.
/// </summary>
/// <param name="Source">The source id (kebab-case), e.g. 'github'.</param>
public sealed record SyncIndexSourceRequest(string Source) : IJob<SyncIndexSourceResult>;
