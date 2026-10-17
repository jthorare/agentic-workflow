using System.Globalization;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>A temp directory standing in for an agent CLI's transcript root: the readers never see a real home.</summary>
internal sealed class UsageFixture : IDisposable
{
    public UsageFixture()
    {
        Root = Directory.CreateTempSubdirectory("aw-usage-").FullName;
    }

    public string Root { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public void Write(string relative, params string[] lines)
    {
        var path = Path.Combine(Root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc));   // after every window a test asks about: the readers skip files untouched since the window opened
    }

    public static string Claude(string id, string at, string model, long input, long cacheRead, long cacheWrite, long output)
    {
        var usage = string.Create(CultureInfo.InvariantCulture, $"{{\"input_tokens\":{input},\"cache_read_input_tokens\":{cacheRead},\"cache_creation_input_tokens\":{cacheWrite},\"output_tokens\":{output}}}");
        return $"{{\"type\":\"assistant\",\"sessionId\":\"s\",\"timestamp\":\"{at}\",\"message\":{{\"id\":\"{id}\",\"model\":\"{model}\",\"usage\":{usage}}}}}";
    }

    public static string CodexModel(string at, string model) => $"{{\"timestamp\":\"{at}\",\"type\":\"turn_context\",\"payload\":{{\"model\":\"{model}\"}}}}";

    public static string CodexTokens(string at, long total, long input, long cached, long output)
    {
        var usage = string.Create(CultureInfo.InvariantCulture, $"{{\"input_tokens\":{input},\"cached_input_tokens\":{cached},\"output_tokens\":{output},\"total_tokens\":{total}}}");
        return $"{{\"timestamp\":\"{at}\",\"type\":\"event_msg\",\"payload\":{{\"type\":\"token_count\",\"info\":{{\"total_token_usage\":{usage},\"last_token_usage\":{usage}}}}}}}";
    }
}
