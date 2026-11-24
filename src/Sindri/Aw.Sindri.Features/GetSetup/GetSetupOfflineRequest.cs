namespace Aw.Sindri.Features.GetSetup;

/// <summary>
/// Where "set up Sindri once" stands when the control plane isn't running ('sindri setup' with serve down; first-run spec decision 8): the steps the CLI can decide in process
/// (serve, repo, profile, agent) from the same <see cref="Core.Setup.SetupSteps"/> rules as GET /sindri/setup, and the other four as waiting for Sindri to run. A pure read that starts nothing.
/// </summary>
public sealed record GetSetupOfflineRequest();
