using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The shape and range rules of 'context.sources.library' (review page spec §5.1), all SND-LIB-001 naming the key path. The rules that need the machine's
/// home folders are <see cref="LibraryPaths"/>'s.
/// </summary>
internal static partial class LibraryRules
{
    private const string Prefix = "context.sources.library";
    private const int MaxVaults = 10;
    private const int MaxDeny = 50;
    private const int MaxGlob = 200;
    private static readonly JsonValueKind[] _boolean = [JsonValueKind.True, JsonValueKind.False];
    private static readonly string[] _keys = ["stateDir", "models", "vaults"];
    private static readonly string[] _vaultKeys = ["name", "path", "deny", "models"];

    /// <summary>The shape of the 'library' entry of a 'sources' map: a map of known keys of the right kinds, so a mistake is named by its key path and not by the deserializer.</summary>
    /// <param name="sources">The 'context.sources' map.</param>
    /// <returns>SND-LIB-001, or null.</returns>
    internal static Error? Shape(JsonElement sources)
    {
        if (!sources.TryGetProperty("library", out var library) || library.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (library.ValueKind != JsonValueKind.Object)
        {
            return Invalid(Prefix, "must be a map (stateDir, models, vaults)");
        }

        return UnknownKey(library, Prefix, _keys)
            ?? Kind(library, "stateDir", JsonValueKind.String, "a folder path or off")
            ?? Kind(library, "models", _boolean, "true or false")
            ?? VaultShapes(library);
    }

    /// <summary>'context.sources.library' checked, or null success when the profile names none.</summary>
    /// <param name="section">The section as written.</param>
    /// <returns>The settings, or SND-LIB-001 naming the key path.</returns>
    internal static Result<LibrarySettings?> Library(LibrarySection? section)
    {
        if (section is null)
        {
            return Kernel.Results.Success<LibrarySettings?>(value: null);
        }

        var vaults = new List<LibraryVault>();
        var written = section.Vaults ?? [];
        var problem = StateDir(section.StateDir) ?? Count(section.Vaults);
        for (var i = 0; problem is null && i < written.Count; i++)
        {
            problem = Vault(i, written[i], vaults);
        }

        return problem is not null
            ? problem
            : Kernel.Results.Success<LibrarySettings?>(new LibrarySettings { StateDir = section.StateDir, Models = section.Models ?? false, Vaults = vaults });
    }

    /// <summary>The root names whose docs may reach a model call.</summary>
    /// <param name="library">The checked entry, or null.</param>
    /// <returns>'aw' when its root is on and 'models' is true, then each vault with 'models: true'.</returns>
    internal static IReadOnlyList<string> ModelRoots(LibrarySettings? library) =>
        library is null ? [] : [.. library is { AwRootOn: true, Models: true } ? new[] { "aw" } : [], .. library.Vaults.Where(static v => v.Models).Select(static v => v.Name)];

    /// <summary>What is wrong with a root path as written, or null: an absolute path with no '..', or one that starts with '~/' (checked as it would be once expanded).</summary>
    /// <param name="path">The path as written.</param>
    /// <returns>The problem, or null.</returns>
    internal static string? PathProblem(string? path) =>
        path switch
        {
            null or "" => "is required: an absolute folder path, or one that starts with ~/",
            _ when path.Any(char.IsControl) => "must not hold control characters",
            _ when path.StartsWith("~/", StringComparison.Ordinal) => PathRules.AbsolutePathProblem(path[1..]) is null ? null : "must be an absolute path (or start with ~/) with no '..'",
            _ => PathRules.AbsolutePathProblem(path) is null ? null : "must be an absolute path (or start with ~/) with no '..'",
        };

    private static Error? StateDir(string? stateDir) =>
        stateDir is null || string.Equals(stateDir, LibrarySettings.Off, StringComparison.Ordinal) || PathProblem(stateDir) is null
            ? null
            : Invalid(Prefix + ".stateDir", $"'{SettingsReader.Printable(stateDir)}' {PathProblem(stateDir)}; or off");

    private static Error? Count(IReadOnlyList<LibraryVaultSection?>? vaults) =>
        vaults is { Count: > MaxVaults }
            ? Invalid(Prefix + ".vaults", string.Create(CultureInfo.InvariantCulture, $"lists {vaults.Count} vaults, at most {MaxVaults}"))
            : null;

    private static Error? Vault(int at, LibraryVaultSection? vault, List<LibraryVault> seen)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{Prefix}.vaults[{at}]");
        if (vault is null)
        {
            return Invalid(key, "must be a map with a name and a path");
        }

        var problem = NameAndPath(key, vault, seen) ?? DenyProblem(key + ".deny", vault.Deny);
        if (problem is null)
        {
            seen.Add(new LibraryVault { Name = vault.Name ?? string.Empty, Path = vault.Path ?? string.Empty, Deny = [.. vault.Deny?.OfType<string>() ?? []], Models = vault.Models ?? false });
        }

        return problem;
    }

    private static Error? NameAndPath(string key, LibraryVaultSection vault, List<LibraryVault> seen) =>
        NameProblem(vault.Name) is { } bad
            ? Invalid(key + ".name", bad)
            : seen.Exists(v => string.Equals(v.Name, vault.Name, StringComparison.Ordinal))
                ? Invalid(key + ".name", $"'{vault.Name}' is used twice")
                : PathProblem(vault.Path) is { } path
                    ? Invalid(key + ".path", vault.Path is null ? path : $"'{SettingsReader.Printable(vault.Path)}' {path}")
                    : null;

    private static string? NameProblem(string? name) =>
        name switch
        {
            null or "" => "is required: 1 to 32 lowercase letters, digits or '-'",
            _ when !NameShape.IsMatch(name) => $"'{SettingsReader.Printable(name)}' isn't a vault name: 1 to 32 lowercase letters, digits or '-'",
            "aw" => "'aw' is the state folder's own root name; pick another",
            _ when name.StartsWith("repo", StringComparison.Ordinal) => $"'{name}' starts with 'repo', which names the repo docs; pick another",
            _ => null,
        };

    private static Error? DenyProblem(string key, IReadOnlyList<string?>? deny)
    {
        if (deny is null)
        {
            return null;
        }

        if (deny.Count > MaxDeny)
        {
            return Invalid(key, string.Create(CultureInfo.InvariantCulture, $"lists {deny.Count} globs, at most {MaxDeny}"));
        }

        var bad = deny.Select((glob, i) => (glob, i)).FirstOrDefault(static e => e.glob is not { Length: > 0 and <= MaxGlob } g || g.Any(char.IsControl) || g.StartsWith('/') || g.Split('/').Contains(".."));
        return bad.glob is null && !deny.Any(static g => g is null)
            ? null
            : Invalid(string.Create(CultureInfo.InvariantCulture, $"{key}[{bad.i}]"), $"must be a folder-relative glob of 1 to {MaxGlob} characters (no leading '/', no '..', no control characters)");
    }

    private static Error? VaultShapes(JsonElement library)
    {
        if (!library.TryGetProperty("vaults", out var vaults) || vaults.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (vaults.ValueKind != JsonValueKind.Array)
        {
            return Invalid(Prefix + ".vaults", "must be a list of vaults, each a map with a name and a path");
        }

        var at = 0;
        foreach (var vault in vaults.EnumerateArray())
        {
            if (VaultShape(vault, string.Create(CultureInfo.InvariantCulture, $"{Prefix}.vaults[{at++}]")) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? VaultShape(JsonElement vault, string key) =>
        vault.ValueKind != JsonValueKind.Object
            ? Invalid(key, "must be a map with a name and a path")
            : UnknownKey(vault, key, _vaultKeys) ?? Kind(vault, "name", JsonValueKind.String, "a name", key) ?? Kind(vault, "path", JsonValueKind.String, "a folder path", key)
                ?? Kind(vault, "models", _boolean, "true or false", key) ?? DenyKind(vault, key);

    private static Error? DenyKind(JsonElement vault, string key) =>
        vault.TryGetProperty("deny", out var deny) && deny.ValueKind != JsonValueKind.Null && (deny.ValueKind != JsonValueKind.Array || deny.EnumerateArray().Any(static g => g.ValueKind != JsonValueKind.String))
            ? Invalid(key + ".deny", "must be a list of globs")
            : null;

    private static Error? UnknownKey(JsonElement map, string key, string[] known) =>
        map.EnumerateObject().Select(static p => p.Name).FirstOrDefault(n => !known.Contains(n, StringComparer.Ordinal)) is { } name
            ? Invalid($"{key}.{SettingsReader.Printable(name)}", $"is not a {key} setting this build knows ({string.Join(", ", known)})")
            : null;

    private static Error? Kind(JsonElement map, string name, JsonValueKind kind, string describe, string key = Prefix) =>
        Kind(map, name, [kind], describe, key);

    private static Error? Kind(JsonElement map, string name, JsonValueKind[] kinds, string describe, string key = Prefix) =>
        map.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && !kinds.Contains(value.ValueKind)
            ? Invalid($"{key}.{name}", $"must be {describe}")
            : null;

    /// <summary>SND-LIB-001 with the key path first.</summary>
    /// <param name="keyPath">The key path.</param>
    /// <param name="problem">What is wrong.</param>
    /// <returns>The error.</returns>
    internal static Error Invalid(string keyPath, string problem) => ContextErrors.LibraryInvalid.With($"{keyPath}: {problem}");

    [GeneratedRegex("^[a-z0-9-]{1,32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NameShape { get; }
}
