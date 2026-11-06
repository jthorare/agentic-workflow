using System.Text;
using System.Text.Json;
using Aw.Context.Core.Records;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Records;

/// <summary>A pull request's facts as the one JSON text column 'source_record.pull_facts' (migration 0030): written whole with the row, read whole with it.</summary>
internal static class PullFactsJson
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(PullFactsJsonContext.Default);

    public static string? Write(PullFacts? facts) => facts is null ? null : Encoding.UTF8.GetString(AwJson.Serialize(facts, _options));

    // why: a text this build can't read (a newer shape, a hand edit) reads as no facts rather than failing the whole record read
    public static PullFacts? Read(string? json)
    {
        if (json is null)
        {
            return null;
        }

        var read = AwJson.Deserialize<PullFacts>(Encoding.UTF8.GetBytes(json), _options);
        return read.IsError ? null : read.Value;
    }
}
