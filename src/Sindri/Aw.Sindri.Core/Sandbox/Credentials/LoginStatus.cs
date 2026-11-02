using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The cheap, side-effect-free answer of <see cref="IModelCredentialSource.Probe"/>.</summary>
/// <param name="State">Ready, missing or expired.</param>
/// <param name="Source">The source that answered.</param>
/// <param name="Detail">What the owner needs to know (never a secret), or null.</param>
/// <param name="ExpiresAt">When the login stops being valid, or null when the source cannot tell. The registry compares it with the time a Step needs.</param>
public sealed record LoginStatus(LoginState State, ModelLoginSource Source, string? Detail = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresAt = null);
