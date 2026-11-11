using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Sindri.Toolkit;

/// <summary>Makes a <see cref="ToolDefinition"/> from a typed handler and the toolkit's JSON context.</summary>
internal static class ToolFactory
{
    public static ToolDefinition Create<TRequest, TResponse>(
        JsonSerializerContext json,
        string name,
        string description,
        Func<TRequest, ToolkitContext, CancellationToken, Task<TResponse>> handler)
    {
        var requestInfo = InfoOf<TRequest>(json);
        var responseInfo = InfoOf<TResponse>(json);
        var input = RequestSchema.Export(requestInfo, markNullableOptional: true);
        var output = RequestSchema.Export(responseInfo, markNullableOptional: false);

        async Task<JsonElement> Invoke(JsonElement arguments, ToolkitContext context, CancellationToken cancellationToken)
        {
            var request = ReadRequest(name, arguments, input.Optional, requestInfo);
            var response = await handler(request, context, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.SerializeToElement(response, responseInfo);
        }

        return new ToolDefinition(name, description, input.Schema, output.Schema, Invoke);
    }

    private static JsonTypeInfo<T> InfoOf<T>(JsonSerializerContext json) =>
        json.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new ToolkitException(
            ToolkitErrors.InvalidDeclaration,
            $"{typeof(T).Name} is not in the toolkit's JSON context: add [JsonSerializable(typeof({typeof(T).Name}))] to it.");

    // why: a nullable member the client left out reads as null, so "nullable means optional" holds when the record has no default for it
    private static T ReadRequest<T>(string tool, JsonElement arguments, IReadOnlyList<string> optional, JsonTypeInfo<T> info)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            HashSet<string> present = new(StringComparer.Ordinal);
            if (arguments.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in arguments.EnumerateObject())
                {
                    _ = present.Add(property.Name);
                    property.WriteTo(writer);
                }
            }

            foreach (var name in optional.Where(n => !present.Contains(n)))
            {
                writer.WriteNull(name);
            }

            writer.WriteEndObject();
        }

        try
        {
            return JsonSerializer.Deserialize(buffer.ToArray(), info)
                ?? throw new ToolkitException(ToolkitErrors.InvalidCall, $"The arguments of '{tool}' are empty.");
        }
        catch (JsonException ex)
        {
            throw new ToolkitException(ToolkitErrors.InvalidCall, $"The arguments of '{tool}' do not match its request: {ex.Message}");
        }
    }
}
