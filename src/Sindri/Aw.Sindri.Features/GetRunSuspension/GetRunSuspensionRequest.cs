using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetRunSuspension;

/// <summary>'GET /launches/{launch}/suspended' (sandbox spec §7, SB16): whether the launch's run is suspended, proven by the launch secret like the session heartbeat.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="Secret">The launch secret (header X-Sindri-Launch-Secret).</param>
public sealed record GetRunSuspensionRequest(LaunchId LaunchId, LaunchSecret Secret) : IQuery<RunSuspensionView>;
