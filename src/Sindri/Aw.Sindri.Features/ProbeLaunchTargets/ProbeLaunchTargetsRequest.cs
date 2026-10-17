using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ProbeLaunchTargets;

/// <summary>Checks the launch targets again now (POST /sindri/launch/targets/probe; UI spec E-3): a job-class command that records 'launch.probed' when the facts changed.</summary>
public sealed record ProbeLaunchTargetsRequest() : IJob<ProbeLaunchTargetsResult>;
