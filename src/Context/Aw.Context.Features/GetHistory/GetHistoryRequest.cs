using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetHistory;

/// <summary>The history lens of one or more anchors.</summary>
/// <param name="Anchors">Node keys, aliases or labels.</param>
/// <param name="Window">The absolute window.</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetHistoryRequest(IReadOnlyList<string> Anchors, HistoryWindow Window, MapRead Read, ContextScope Scope);
