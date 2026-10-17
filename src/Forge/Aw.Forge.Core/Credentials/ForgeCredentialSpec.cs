using Aw.Kernel;

namespace Aw.Forge.Core.Credentials;

/// <summary>
/// The profile's workflow.forge.credential: "env:AW_FORGE_TOKEN" (a variable name) or "command:&lt;argv&gt;" (the program and its
/// arguments split on spaces; no quoting, no shell). The credential itself is never in this value.
/// </summary>
/// <param name="Kind">Env or command.</param>
/// <param name="Name">The variable name, for Env.</param>
/// <param name="Argv">The program and arguments, for Command.</param>
public sealed record ForgeCredentialSpec(ForgeCredentialKind Kind, string? Name, IReadOnlyList<string> Argv)
{
    /// <summary>The documented default, env:AW_FORGE_TOKEN.</summary>
    public static ForgeCredentialSpec Default { get; } = new(ForgeCredentialKind.Env, "AW_FORGE_TOKEN", []);

    /// <summary>Parses the profile value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The spec, or the refusal.</returns>
    public static Result<ForgeCredentialSpec> Parse(string? value)
    {
        if (value is not null && value.StartsWith("env:", StringComparison.Ordinal))
        {
            var name = value[4..];
            return name.Length > 0 && name.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')
                ? new ForgeCredentialSpec(ForgeCredentialKind.Env, name, [])
                : ForgeErrors.Invalid.With("credential: env:NAME with capitals, digits and '_'");
        }

        if (value is not null && value.StartsWith("command:", StringComparison.Ordinal))
        {
            var argv = value[8..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return argv.Length > 0
                ? new ForgeCredentialSpec(ForgeCredentialKind.Command, Name: null, Argv: argv)
                : ForgeErrors.Invalid.With("credential: command: needs a program");
        }

        return ForgeErrors.Invalid.With("credential: env:NAME or command:<argv>");
    }
}
