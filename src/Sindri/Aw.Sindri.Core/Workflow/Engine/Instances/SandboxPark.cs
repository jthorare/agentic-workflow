namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>The sandbox refusal a park carries (sandbox spec §6): the code, its reason and, for -017, the credential source.</summary>
/// <param name="Code">'SND-LAUNCH-014' to '-017'.</param>
/// <param name="Reason">The refusal's reason ('no-bwrap', 'canary' …), or null when the launcher gave none.</param>
/// <param name="Source">The credential source for -017 ('owner-copy' or 'separate-login'), otherwise null.</param>
public sealed record SandboxPark(string Code, string? Reason, string? Source);
