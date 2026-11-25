namespace Aw.Sindri.Core.Setup;

/// <summary>Where the hello-sindri run stands (first-run spec §3.1, step 8).</summary>
/// <param name="Succeeded">True when a live run of hello-sindri succeeded.</param>
/// <param name="Dismissed">True when the profile holds `setup.skipFirstWorkflow`.</param>
public sealed record FirstWorkflowFact(bool Succeeded, bool Dismissed);
