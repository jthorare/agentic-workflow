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
    /// <param name="box">The box's limits the profile must stay inside (the heavy-slot cap), or null to check the profile alone.</param>
    /// <returns>The loaded profile, or SND-PROFILE-002 naming the file, the key path and the fix.</returns>
    internal static Result<LoadedProfile> FromJson(ReadOnlySpan<byte> json, string source, JsonSerializerOptions options, BoxLimits? box = null)
    {
        var typed = AwJson.Deserialize<SindriProfile>(json, options);
        if (typed.IsError)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: {ProfileErrors.Explain(typed.Error.Message, options)} ({Fix})");
        }

        var checkedProfile = ProfileRules.Check(typed.Value, box);
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
    internal static byte[] Canonical(SindriProfile profile, JsonSerializerOptions options) =>
        AwJson.Serialize(profile.Context is { } context ? profile with { Context = Sorted(context) } : profile, options);

    /// <summary>
    /// The 'context:' section with every object's keys in ordinal order, so the hash doesn't depend on how the section
    /// was written (the rest of the canonical JSON is a typed record, already in a fixed order).
    /// </summary>
    private static JsonElement Sorted(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, element);
        }

        using var sorted = JsonDocument.Parse(buffer.ToArray());
        return sorted.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                Write(writer, item);
            }

            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }
}
