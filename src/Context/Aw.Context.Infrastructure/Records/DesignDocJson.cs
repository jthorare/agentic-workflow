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
}
