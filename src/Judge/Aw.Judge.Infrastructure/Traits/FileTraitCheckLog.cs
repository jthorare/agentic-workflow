using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>ITraitCheckLog over '&lt;judge dir&gt;/trait-check/log.jsonl': each entry gets its time ('ts') first.</summary>
/// <param name="directory">'&lt;judge dir&gt;/trait-check'.</param>
/// <param name="clock">The time of an entry.</param>
internal sealed class FileTraitCheckLog(string directory, TimeProvider clock) : ITraitCheckLog
{
    private readonly Lock _write = new();

    /// <inheritdoc />
    public void Append(JsonObject entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = new JsonObject { ["ts"] = clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture) };
        foreach (var (name, value) in entry)
        {
            line[name] = value?.DeepClone();
        }

        try
        {
            lock (_write)
            {
                _ = Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "log.jsonl"), line.ToJsonString() + "\n");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The log is best effort.
        }
    }
}
