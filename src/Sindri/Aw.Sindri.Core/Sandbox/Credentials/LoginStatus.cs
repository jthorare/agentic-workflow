namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The cheap, side-effect-free answer of <see cref="IModelCredentialSource.Probe"/>.</summary>
/// <param name="State">Ready, missing or expired.</param>
/// <param name="Source">The source that answered.</param>
/// <param name="Detail">What the owner needs to know (never a secret), or null.</param>
public sealed record LoginStatus(LoginState State, ModelLoginSource Source, string? Detail = null);
