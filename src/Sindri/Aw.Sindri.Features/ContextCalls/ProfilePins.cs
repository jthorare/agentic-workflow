using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
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

    /// <summary>The id each entry has in the map the profile builds (<see cref="PinSlug.IdsIn"/>, the rule the build and the per-pin hashes use): two id-less entries that slug alike are 'foo' and 'foo-2'.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="pins">Its entries, from <see cref="Of"/>.</param>
    /// <returns>One id per entry, in order.</returns>
    internal static IReadOnlyList<string> Ids(SindriProfile profile, IReadOnlyList<ProfilePin> pins)
    {
        var scope = ContextScopes.For(profile, new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
        var repos = SettingsReader.Read(scope.Settings) is { IsSuccess: true } settings ? ScopeRules.Mapped(settings.Value, scope.Repos) : scope.Repos;
        return PinSlug.IdsIn([.. repos.SelectMany(static r => r.Concepts)], [.. pins.Select(static p => ContextScopes.PinOf(p.Settings))]);
    }

    private static IEnumerable<ProfilePin> Entries(string prefix, string setIn, string? repo, IndexSettings? index) =>
        (index?.Concepts ?? []).Select((pin, position) => new ProfilePin($"{prefix}.concepts.{pin.Id?.Value ?? position.ToString(CultureInfo.InvariantCulture)}", setIn, repo, pin));
}
