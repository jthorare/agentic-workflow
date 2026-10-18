namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>One bind that makes up the agent home.</summary>
/// <param name="HostPath">The path on the host.</param>
/// <param name="SandboxPath">Where it appears in the sandbox.</param>
/// <param name="Mode">Read-only or read-write.</param>
public sealed record LoginMount(string HostPath, string SandboxPath, MountMode Mode);
