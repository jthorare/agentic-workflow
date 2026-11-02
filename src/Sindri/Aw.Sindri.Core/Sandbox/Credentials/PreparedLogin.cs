namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>
/// A prepared login: plain data the pure plan builder consumes (sandbox spec SB18).
/// </summary>
/// <param name="Source">The source that prepared it.</param>
/// <param name="Mounts">The mounts that make up the agent home.</param>
/// <param name="Environment">The environment names and values to add (CLAUDE_CONFIG_DIR, CODEX_HOME).</param>
/// <param name="DeniedReads">The paths the inner policy must deny reading (the login copy).</param>
/// <param name="Handle">The cleanup handle for <see cref="IModelCredentialSource.Release"/>.</param>
public sealed record PreparedLogin(
    ModelLoginSource Source,
    IReadOnlyList<LoginMount> Mounts,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> DeniedReads,
    LoginHandle Handle);
