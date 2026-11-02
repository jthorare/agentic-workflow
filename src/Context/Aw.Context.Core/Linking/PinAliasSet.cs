using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Core.Linking;

/// <summary>The approved pins of a scope as the alias rule reads them, and the hash of what they say (concept pins spec §3.4): the engine's relink and the Sindri tick that decides to queue it both call it.</summary>
public static class PinAliasSet
{
    private const char Unit = '\u001f';

    /// <summary>The pins of the mapped repos (a pin repeated once per repo is one), each with the id its concept node carries, in the build's order.</summary>
    /// <param name="scope">The profile's settings and repos.</param>
    /// <returns>The pins and their aliases, or SND-CONTEXT-001.</returns>
    public static Result<IReadOnlyList<PinAliases>> Of(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var pins = ScopeRules.Mapped(settings.Value, scope.Repos).SelectMany(static r => r.Concepts);
        return PinIds.Resolve(pins).Select(static p => new PinAliases(ConceptId.From(p.Id), p.Pin.Aliases)).ToList();
    }

    /// <summary>The lower-case hex SHA-256 over the pins in id order and each pin's aliases in ordinal order; no pins is still a hash (of nothing), so a relink with none removes stale alias links.</summary>
    /// <param name="pins">The pins.</param>
    /// <returns>The hash.</returns>
    public static string Hash(IReadOnlyList<PinAliases> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        var lines = pins.OrderBy(static p => p.Id.Value, StringComparer.Ordinal)
            .Select(static p => string.Join(Unit, [p.Id.Value, .. p.Aliases.Order(StringComparer.Ordinal)]));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }
}
