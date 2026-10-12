using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>JSON bytes to a checked, hashed profile: AwJson for the shape, ProfileRules for the rest, the canonical JSON for the hash.</summary>
internal static class ProfileReading
{
    internal const string Fix = "fix: edit the file; keys and types are in docs/csharp/sindri.md";

    /// <summary>The checked profile from <paramref name="json"/>, with its hash.</summary>
    /// <param name="json">The profile as JSON.</param>
    /// <param name="source">The file it came from, for messages.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The loaded profile, or SND-PROFILE-002 naming the file, the key path and the fix.</returns>
    internal static Result<LoadedProfile> FromJson(ReadOnlySpan<byte> json, string source, JsonSerializerOptions options)
    {
        var typed = AwJson.Deserialize<SindriProfile>(json, options);
        if (typed.IsError)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: {ProfileErrors.Explain(typed.Error.Message, options)} ({Fix})");
        }

        var checkedProfile = ProfileRules.Check(typed.Value);
        if (checkedProfile.IsError)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: {checkedProfile.Error.Message} ({Fix})");
        }

        var canonical = Canonical(checkedProfile.Value, options);
        return new LoadedProfile(checkedProfile.Value, ProfileHashing.Of(canonical), source);
    }

    /// <summary>The canonical JSON of a checked profile: what the hash covers and what an approval snapshot stores.</summary>
    /// <param name="profile">The checked profile.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The UTF-8 JSON.</returns>
    internal static byte[] Canonical(SindriProfile profile, JsonSerializerOptions options) => AwJson.Serialize(profile, options);
}
