namespace Aw.Judge.Core.Secrets;

/// <summary>
/// The environment variable each secret is read from: plain data, so every adapter that needs it (a secret source
/// reading it, an agent CLI unsetting it for its child) reads it here and none depends on another.
/// </summary>
public static class SecretNames
{
    /// <summary>The variable of every secret the judge owns (what an agent CLI child must not inherit).</summary>
    public static IReadOnlyList<string> Variables { get; } = [.. Enum.GetValues<SecretName>().Select(Variable)];

    /// <summary>The environment variable a secret is read from.</summary>
    /// <param name="name">The secret.</param>
    /// <returns>Its variable.</returns>
    public static string Variable(SecretName name) => name switch
    {
        SecretName.TypeSafeApiKey => "TYPESAFE_API_KEY",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown secret"),
    };
}
