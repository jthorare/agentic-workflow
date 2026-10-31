using System.Text;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands.Pins;

/// <summary>The profile's pins and its live hash, as 'context pin list --json' prints them.</summary>
/// <param name="Hash">The live profile's hash: the one to pass to --if-hash.</param>
/// <param name="Pins">Every 'index.concepts' entry of the profile and of its repos, in file order.</param>
internal sealed record PinList(string Hash, IReadOnlyList<PinListEntry> Pins)
{
    internal static PinList From(LoadedProfile loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var profile = loaded.Profile;
        return new PinList(loaded.Hash.Value,
        [
            .. (profile.Index?.Concepts ?? []).Select(static c => PinListEntry.From(c, "profile")),
            .. (profile.Repos ?? []).SelectMany(static r => (r.Index?.Concepts ?? []).Select(c => PinListEntry.From(c, $"repo {r.Name}"))),
        ]);
    }

    internal static string Render(PinList view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder().Append("hash ").Append(view.Hash).Append('\n');
        foreach (var pin in view.Pins)
        {
            _ = text.Append(pin.Id ?? "-").Append("  ").Append(pin.Name).Append("  set in ").Append(pin.SetIn)
                .Append(pin.Aliases.Count > 0 ? "  aliases: " + string.Join(", ", pin.Aliases) : string.Empty)
                .Append(pin.Features.Count > 0 ? "  features: " + string.Join(", ", pin.Features) : string.Empty).Append('\n');
        }

        return (view.Pins.Count > 0 ? text : text.Append("no pins\n")).ToString();
    }
}
