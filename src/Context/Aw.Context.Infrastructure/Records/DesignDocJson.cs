using System.Text;
using System.Text.Json;
using Aw.Context.Core.Library;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The JSON text columns of 'design_doc': written whole with the row.</summary>
internal static class DesignDocJson
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(DesignDocJsonContext.Default);

    internal static string Strings(IReadOnlyList<string>? values) => Encoding.UTF8.GetString(AwJson.Serialize(values is null ? [] : values.ToArray(), _options));

    internal static string Errors(IReadOnlyList<DesignDocError> errors) => Encoding.UTF8.GetString(AwJson.Serialize(errors.ToArray(), _options));

    // why: a text this build can't read (a hand edit) reads as none rather than failing the whole listing
    internal static IReadOnlyList<string> ReadStrings(string json)
    {
        var read = AwJson.Deserialize<string[]>(Encoding.UTF8.GetBytes(json), _options);
        return read.IsError ? [] : read.Value;
    }

    internal static IReadOnlyList<DesignDocError> ReadErrors(string json)
    {
        var read = AwJson.Deserialize<DesignDocError[]>(Encoding.UTF8.GetBytes(json), _options);
        return read.IsError ? [] : read.Value;
    }
}
