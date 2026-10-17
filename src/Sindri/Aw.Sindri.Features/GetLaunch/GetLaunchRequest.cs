using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunch;

/// <summary>One launch (GET /sindri/launches/{launchId}; UI spec E-1).</summary>
/// <param name="LaunchId">The launch.</param>
public sealed record GetLaunchRequest(LaunchId LaunchId) : IQuery<LaunchView>;
