using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.RecordLimitHit;

/// <summary>'POST /launches/{launch}/limit-hit' (sandbox spec §3.3): 'session wrap' says the scope reached a limit that does not end the session (pids: forks fail with EAGAIN), proven by the launch secret.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="Secret">The launch secret (header X-Sindri-Launch-Secret).</param>
/// <param name="Kind">Which limit.</param>
/// <param name="Peak">The peak: a count for pids, bytes for memory and disk.</param>
public sealed record RecordLimitHitRequest(LaunchId LaunchId, LaunchSecret Secret, LimitHitKind Kind, long Peak) : IIssue<RecordLimitHitResponse>;
