using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// A pointer to a secret held outside the profile (context spec §4 "Secrets", §8.8): 'env:NAME',
/// 'file:/absolute/path', 'keychain:&lt;service&gt;/&lt;account&gt;' or 'op:&lt;vault&gt;/&lt;item&gt;/&lt;field&gt;'.
/// The target is not secret; the value it points at is.
/// </summary>
public sealed record SecretPointer
{
    private SecretPointer(SecretScheme scheme, string target)
    {
        Scheme = scheme;
        Target = target;
    }

    /// <summary>Where the value lives.</summary>
    public SecretScheme Scheme { get; }

    /// <summary>The scheme's target: the variable name, the path, 'service/account' or 'vault/item/field'.</summary>
    public string Target { get; }

    /// <summary>Whether a name passed to the environment function is a pointer: it holds a ':', which no environment variable name does.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True for a pointer.</returns>
    public static bool IsPointer(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Contains(':', StringComparison.Ordinal);
    }

    /// <summary>Parses a pointer. The error never repeats the text after the scheme (it may be a secret pasted by mistake).</summary>
    /// <param name="text">The pointer text from the profile.</param>
    /// <returns>The pointer, or SND-SECRET-001.</returns>
    public static Result<SecretPointer> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return Invalid("The secret pointer has no scheme");
        }

        var target = text[(colon + 1)..];
        return text[..colon] switch
        {
            "env" => Checked(SecretScheme.Env, target, IsVariableName(target)),
            "file" => Checked(SecretScheme.File, target, target.StartsWith('/') && !target.Contains('\0', StringComparison.Ordinal)),
            "keychain" => Checked(SecretScheme.Keychain, target, AreSegments(target, 2)),
            "op" => Checked(SecretScheme.OnePassword, target, AreSegments(target, 3)),
            var scheme => Invalid(IsWord(scheme) ? $"The secret pointer's scheme '{scheme}' is unknown" : "The secret pointer's scheme is unknown"),
        };
    }

    /// <summary>The pointer as the profile writes it, e.g. 'keychain:sindri-index/github'. Holds no secret.</summary>
    /// <returns>The pointer text.</returns>
    public override string ToString() => $"{Name(Scheme)}:{Target}";

    private static string Name(SecretScheme scheme) => scheme switch
    {
        SecretScheme.Env => "env",
        SecretScheme.File => "file",
        SecretScheme.Keychain => "keychain",
        SecretScheme.OnePassword => "op",
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "unknown scheme"),
    };

    private static Result<SecretPointer> Checked(SecretScheme scheme, string target, bool valid)
        => valid ? new SecretPointer(scheme, target) : Invalid($"The {Name(scheme)}: pointer's target is malformed");

    private static Error Invalid(string what) => SindriErrors.SecretPointerInvalid.With(
        what + ". Expected env:NAME (letters, digits, '_'; not starting with a digit), file:/absolute/path, "
        + "keychain:<service>/<account> or op:<vault>/<item>/<field> (segments non-empty, none starting with '-').");

    private static bool IsVariableName(string name)
        => name.Length > 0 && !char.IsAsciiDigit(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static bool AreSegments(string target, int count)
    {
        var segments = target.Split('/');
        return segments.Length == count && segments.All(s => s.Length > 0 && s[0] != '-');
    }

    // A short lowercase word is safe to echo as the scheme seen; anything else might be part of a secret.
    private static bool IsWord(string scheme) => scheme.Length is > 0 and <= 12 && scheme.All(char.IsAsciiLetterLower);
}
