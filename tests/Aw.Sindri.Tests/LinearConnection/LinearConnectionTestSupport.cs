using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>The live profile every Connections service test reads its hash from.</summary>
internal static class LinearConnectionTestSupport
{
    /// <summary>Gets the live profile in memory.</summary>
    /// <returns>The profiles, with a live profile that has a hash.</returns>
    internal static InMemoryProfiles Profiles()
    {
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse("{}").RootElement.Clone());
        return new InMemoryProfiles { Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml") };
    }

    /// <summary>A guard over <paramref name="profiles"/> whose live profile is also its approved one: no url change waits.</summary>
    /// <param name="profiles">The profiles, with a live profile set.</param>
    /// <returns>The guard.</returns>
    internal static LinearUrlGuard Guard(InMemoryProfiles profiles)
    {
        _ = profiles.Save(profiles.Live.Value, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return new LinearUrlGuard(profiles, profiles);
    }

    /// <summary>Makes the Linear url a change that waits: the profile approved now has none, and the live one names "http://127.0.0.1:9/graphql".</summary>
    /// <param name="profiles">The profiles, with a live profile set.</param>
    /// <returns>A guard over them.</returns>
    internal static LinearUrlGuard Pending(InMemoryProfiles profiles)
    {
        _ = profiles.Save(profiles.Live.Value, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse("""{"sources":{"linear":{"url":"http://127.0.0.1:9/graphql"}}}""").RootElement.Clone());
        profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
        return new LinearUrlGuard(profiles, profiles);
    }
}
