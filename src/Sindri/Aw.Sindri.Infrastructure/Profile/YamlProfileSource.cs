using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The live profile: a YAML file read through <see cref="YamlToJson"/> and AwJson, checked against the box's limits (<paramref name="box"/>, read each time: the cap can change).</summary>
internal sealed class YamlProfileSource(SindriSettings settings, JsonSerializerOptions options, Func<BoxLimits>? box = null) : IProfileSource
{
    public async ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken)
    {
        var path = settings.ProfilePath;
        if (!File.Exists(path))
        {
            return SindriErrors.ProfileMissing.With(
                $"{path} doesn't exist (fix: create it with 'schemaVersion: 1' and 'user: <you>'; keys are in docs/csharp/sindri.md)");
        }

        string yaml;
        try
        {
            yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException e)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }

        return YamlToJson.TryConvert(yaml, out var json) is { } problem
            ? SindriErrors.ProfileInvalid.With($"{path}: {problem} ({ProfileReading.Fix})")
            : ProfileReading.FromJson(json, path, options, box?.Invoke());
    }
}
