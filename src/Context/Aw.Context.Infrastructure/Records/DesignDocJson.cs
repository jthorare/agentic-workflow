using System.Text;
using System.Text.Json;
using Aw.Context.Core.Library;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The JSON text columns of 'design_doc' and the label list of 'linear_issue': written whole with the row.</summary>
internal static class DesignDocJson
{
    internal static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(DesignDocJsonContext.Default);

    internal static string Strings(IReadOnlyList<string>? values) => Encoding.UTF8.GetString(AwJson.Serialize(values is null ? [] : values.ToArray(), Options));

    internal static string Errors(IReadOnlyList<DesignDocError> errors) => Encoding.UTF8.GetString(AwJson.Serialize(errors.ToArray(), Options));
}
