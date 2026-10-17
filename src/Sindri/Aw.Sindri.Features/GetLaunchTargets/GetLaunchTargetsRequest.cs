using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.GetLaunchTargets;

/// <summary>What this box can launch (GET /sindri/launch/targets; UI spec E-3, screens spec E-28).</summary>
public sealed record GetLaunchTargetsRequest() : IQuery<LaunchTargets>;
