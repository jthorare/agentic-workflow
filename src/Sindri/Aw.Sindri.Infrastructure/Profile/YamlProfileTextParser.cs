using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The reader <see cref="YamlProfileSource"/> uses, over text that isn't on disk yet: YAML to JSON, then AwJson and the profile's rules against the box's limits.</summary>
internal sealed class YamlProfileTextParser(JsonSerializerOptions options, Func<BoxLimits>? box = null) : IProfileTextParser
{
    public Result<LoadedProfile> Parse(string yaml, string source)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(source);
        return YamlToJson.TryConvert(yaml, out var json) is { } problem
            ? SindriErrors.ProfileInvalid.With($"{source}: {problem} ({ProfileReading.Fix})")
            : ProfileReading.FromJson(json, source, options, box?.Invoke());
    }
}
