using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Onyx;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Reads 'context.sources' for <see cref="SettingsReader"/> (spec §8.8): 'onyx' through <see cref="OnyxSettings.Create"/>
/// (SND-CONTEXT-055 or 002 with the key path) and 'linear' (its teams, and with a token the native adapter's settings through <see cref="LinearSettings.Create"/>). The shape (a map, known keys only) is SND-CONTEXT-001
/// naming the full key path.
/// </summary>
internal static partial class SourcesReader
{
    private static readonly string[] _sources = ["onyx", "linear"];
    private static readonly string[] _onyx = ["url", "token", "version", "managed", "network"];
    private static readonly string[] _linear = ["teams", "url", "token"];

    /// <summary>The first problem with the shape of <paramref name="context"/>'s 'sources' map, or null.</summary>
    /// <param name="context">The whole 'context:' section (a map).</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Shape(JsonElement context)
    {
        if (!context.TryGetProperty("sources", out var sources) || sources.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (sources.ValueKind != JsonValueKind.Object)
        {
            return SettingsReader.Invalid("context.sources", "must be a map of sources (onyx, linear)");
        }

        return Unknown(sources, _sources) is { } name
            ? SettingsReader.Invalid("context.sources." + name, "is not a source this build knows (onyx, linear)")
            : Entry(sources, "onyx", _onyx) ?? Entry(sources, "linear", _linear);
    }

    /// <summary>The checked onyx settings (or null) and Linear teams of a well-shaped 'sources' map.</summary>
    /// <param name="section">The section as read, or null.</param>
    /// <returns>The values, or SND-CONTEXT-001, 002 or 055 naming the key path.</returns>
    internal static Result<(OnyxSettings? Onyx, LinearSettings? Linear, IReadOnlyList<string> Teams)> Check(SourcesSection? section)
    {
        var linear = section?.Linear;
        var teams = linear?.Teams ?? [];
        if (BadTeam(teams) is { } problem)
        {
            return problem;
        }

        var native = NativeLinear(linear, teams);
        var onyx = CheckedOnyx(section?.Onyx);
        return (native, onyx) switch
        {
            ({ IsError: true }, _) => native.Error,
            (_, { IsError: true }) => onyx.Error,
            _ => (onyx.Value, native.Value, teams),
        };
    }

    private static Result<OnyxSettings?> CheckedOnyx(OnyxSection? onyx)
    {
        if (onyx is null)
        {
            return Kernel.Results.Success<OnyxSettings?>(value: null);
        }

        var created = OnyxSettings.Create(onyx.Url, onyx.Token, onyx.Version, onyx.Managed ?? false, onyx.Network);
        return created.IsError ? created.Error : Kernel.Results.Success<OnyxSettings?>(created.Value);
    }

    private static Error? BadTeam(IReadOnlyList<string> teams) =>
        teams.FirstOrDefault(t => !TeamKey.IsMatch(t)) is { } bad
            ? SettingsReader.Invalid("context.sources.linear.teams",
                $"'{SettingsReader.Printable(bad)}' isn't a Linear team key (a capital letter, then one or more capitals or digits, e.g. ENG)")
            : null;

    /// <summary>The native adapter's settings when the entry names a token or a url; null for teams alone.</summary>
    private static Result<LinearSettings?> NativeLinear(LinearSection? linear, IReadOnlyList<string> teams)
    {
        if (linear is not ({ Token: not null } or { Url: not null }))
        {
            return Kernel.Results.Success<LinearSettings?>(value: null);
        }

        var created = LinearSettings.Create(linear.Url, linear.Token, teams);
        return created.IsError ? created.Error : Kernel.Results.Success<LinearSettings?>(created.Value);
    }

    private static Error? Entry(JsonElement sources, string name, string[] known)
    {
        if (!sources.TryGetProperty(name, out var entry) || entry.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var keys = string.Join(", ", known);
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return SettingsReader.Invalid($"context.sources.{name}", $"must be a map ({keys})");
        }

        return Unknown(entry, known) is { } key
            ? SettingsReader.Invalid($"context.sources.{name}.{key}", $"is not a context.sources.{name} setting this build knows ({keys})")
            : null;
    }

    private static string? Unknown(JsonElement map, string[] known) =>
        map.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !known.Contains(n, StringComparer.Ordinal)) is { } name
            ? SettingsReader.Printable(name)
            : null;

    [GeneratedRegex("^" + OnyxRecordKeys.TeamKeyPattern + "\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TeamKey { get; }
}
