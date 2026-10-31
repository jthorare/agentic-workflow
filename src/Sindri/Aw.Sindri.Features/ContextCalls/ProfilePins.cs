using System.Globalization;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Every 'index.concepts' entry of a profile and of its repos, in file order: the one reading of the pin list that 'context pin list' and GET /sindri/context/pins share.</summary>
internal static class ProfilePins
{
    internal static IReadOnlyList<ProfilePin> Of(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return
        [
            .. Entries("index", "profile", repo: null, profile.Index),
            .. (profile.Repos ?? []).SelectMany(r => Entries($"repos.{r.Name}.index", $"repo {r.Name}", r.Name, r.Index)),
        ];
    }

    private static IEnumerable<ProfilePin> Entries(string prefix, string setIn, string? repo, IndexSettings? index) =>
        (index?.Concepts ?? []).Select((pin, position) => new ProfilePin($"{prefix}.concepts.{pin.Id?.Value ?? position.ToString(CultureInfo.InvariantCulture)}", setIn, repo, pin));
}
