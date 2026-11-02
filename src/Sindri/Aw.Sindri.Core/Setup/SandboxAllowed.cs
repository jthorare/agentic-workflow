namespace Aw.Sindri.Core.Setup;

/// <summary>What <see cref="IProfileSandboxWriter.Allow"/> wrote.</summary>
/// <param name="Entry">The value now in the profile: the host, or the path relative to the home.</param>
/// <param name="Added">False when the live profile already held it (nothing was written).</param>
public sealed record SandboxAllowed(string Entry, bool Added);
