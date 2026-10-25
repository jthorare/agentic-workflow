using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// Turns AwJson's deserialization message into one a profile author can act on: the key path, what the key expects in
/// YAML words, or which keys are allowed, without the JSON byte positions (they mean nothing in a YAML file).
/// </summary>
internal static partial class ProfileErrors
{
    internal static string Explain(string message, JsonSerializerOptions options)
    {
        if (Unmapped.Match(message) is { Success: true } unmapped)
        {
            var keys = Keys(options, unmapped.Groups["type"].Value);
            return $"'{unmapped.Groups["name"].Value}' is not a profile key here{(keys.Length == 0 ? string.Empty : $" (allowed: {keys})")}";
        }

        if (Converted.Match(message) is { Success: true } converted)
        {
            var path = converted.Groups["path"].Value.TrimStart('$').TrimStart('.');
            return $"{path}: expected {Expected(options, path)}";
        }

        return Missing.Match(message) is { Success: true } missing
            ? $"{missing.Groups["names"].Value.Replace("'", string.Empty, StringComparison.Ordinal)}: required"
            : Positions.Replace(message, string.Empty);
    }

    private static string Keys(JsonSerializerOptions options, string typeName)
    {
        var info = Known(options).FirstOrDefault(t => string.Equals(t.Type.FullName, typeName, StringComparison.Ordinal));
        return info is null ? string.Empty : string.Join(", ", info.Properties.Select(p => p.Name));
    }

    private static string Expected(JsonSerializerOptions options, string path)
    {
        var info = options.GetTypeInfo(typeof(SindriProfile));
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = info.Kind switch
            {
                JsonTypeInfoKind.Object => info.Properties.FirstOrDefault(p => string.Equals(p.Name, segment, StringComparison.Ordinal))?.PropertyType,
                JsonTypeInfoKind.Dictionary => info.Type.GetGenericArguments()[^1],
                JsonTypeInfoKind.Enumerable => info.ElementType,
                JsonTypeInfoKind.None => null,
                _ => null,
            };
            if (next is null)
            {
                break;
            }

            info = options.GetTypeInfo(Nullable.GetUnderlyingType(next) ?? next);
        }

        return Words(info);
    }

    private static string Words(JsonTypeInfo info) => info.Kind switch
    {
        JsonTypeInfoKind.Object => "a mapping",
        JsonTypeInfoKind.Dictionary => "a mapping",
        JsonTypeInfoKind.Enumerable => "a list",
        JsonTypeInfoKind.None => Primitive(info.Type),
        _ => "text",
    };

    private static string Primitive(Type type) => type switch
    {
        _ when type == typeof(int) || type == typeof(long) => "a whole number",
        _ when type == typeof(bool) => "true or false",
        _ => "text",
    };

    private static IEnumerable<JsonTypeInfo> Known(JsonSerializerOptions options) =>
        new[] { typeof(SindriProfile), typeof(RepoEntry), typeof(ServiceSettings), typeof(ApiSettings), typeof(IndexSettings), typeof(EmbeddingSettings), typeof(GraphSettings), typeof(ConceptSettings) }.Select(options.GetTypeInfo);

    [GeneratedRegex(@"The JSON property '(?<name>[^']+)' could not be mapped to any \.NET member contained in type '(?<type>[^']+)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Unmapped { get; }

    [GeneratedRegex(@"could not be converted to \S+?\.? Path: (?<path>\$[^ |]*)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Converted { get; }

    [GeneratedRegex(@"missing required properties including: (?<names>.+?)\.?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Missing { get; }

    [GeneratedRegex(@" ?\| LineNumber: \d+ \| BytePositionInLine: \d+\.?", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Positions { get; }
}
