using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The compose project `sindri onyx up` runs: its name and three files, named so every command gets the same arguments.</summary>
/// <param name="Name">The compose project name every command passes with -p, one per Sindri home (see <see cref="NameFor"/>), so two homes on one Docker never touch each other's stack and Sindri's Onyx never mixes with another stack.</param>
/// <param name="ComposeFile">The pinned docker-compose.yml.</param>
/// <param name="OverrideFile">Sindri's docker-compose.override.yml.</param>
/// <param name="EnvFile">The .env Sindri rendered.</param>
public sealed record ComposeProject(string Name, string ComposeFile, string OverrideFile, string EnvFile)
{
    private const string Prefix = "sindri-onyx-";
    private const int HashHexDigits = 8;

    /// <summary>Gets whether the project names its compose, override and .env files (false for <see cref="ByLabel"/>).</summary>
    public bool HasFiles => ComposeFile.Length > 0;

    /// <summary>Gets the variables every compose call must not inherit from the shell: Compose's own COMPOSE_* settings and every key Sindri writes into the .env (the shell wins over the .env, so a stray one would change what the rendered files say).</summary>
    public static IReadOnlyList<string> StrippedVariables { get; } = ["COMPOSE_PROFILES", "COMPOSE_FILE", "COMPOSE_PROJECT_NAME", "COMPOSE_ENV_FILES", "COMPOSE_PATH_SEPARATOR", .. OnyxConfig.EnvKeys];

    /// <summary>Names the compose project of a Sindri home: `sindri-onyx-` and the first 8 hex digits of the sha256 of the home's full path. The one place the name is computed.</summary>
    /// <param name="sindriHome">The Sindri home (AW_SINDRI_HOME) as a full path; a trailing separator doesn't change the name.</param>
    /// <returns>For example sindri-onyx-0a1b2c3d.</returns>
    public static string NameFor(string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sindriHome.TrimEnd('/', '\\'))));
        return Prefix + hash[..HashHexDigits];
    }

    /// <summary>Names a project with no files: compose identifies its containers by the project label, which is enough to list and stop them when Sindri's own files are gone.</summary>
    /// <param name="name">The compose project name (see <see cref="NameFor"/>).</param>
    /// <returns>The project with empty file paths.</returns>
    public static ComposeProject ByLabel(string name) => new(name, "", "", "");

    /// <summary>Tells whether a name read back from state.json is one <see cref="NameFor"/> could have made (so a hand-edited file can't smuggle an option or another project into a docker command).</summary>
    /// <param name="name">The recorded name.</param>
    /// <returns>True for `sindri-onyx-` and 8 lowercase hex digits.</returns>
    public static bool IsOurs(string? name)
        => name is not null && name.Length == Prefix.Length + HashHexDigits && name.StartsWith(Prefix, StringComparison.Ordinal) && name[Prefix.Length..].All(static c => char.IsAsciiHexDigitLower(c));
}
