namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>What <see cref="IModelCredentialSource.Release"/> needs to clean a prepared login up (the per-launch copy's directory, for the owner-copy source).</summary>
/// <param name="Value">An opaque value the source understands.</param>
public sealed record LoginHandle(string Value);
