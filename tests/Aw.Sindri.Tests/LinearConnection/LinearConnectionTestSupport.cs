using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
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
}
