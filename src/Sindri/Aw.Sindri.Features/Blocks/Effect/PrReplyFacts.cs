using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// What the PR effects read from their context: an argument or an instance fact reaches an effect as the stored reference ('{ref, sha256}'),
/// and these read its content (JSON, or the text it is) and pull a text out of it.
/// </summary>
internal static class PrReplyFacts
{
    /// <summary>An argument's value: the content when it is a stored fact, else the literal; an undefined element when it is absent.</summary>
    /// <param name="facts">The fact content port.</param>
    /// <param name="context">The effect's context.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value, or the error reading it.</returns>
    public static ValueTask<Result<JsonElement>> Argument(IFactContents facts, EffectContext context, string name, CancellationToken cancellationToken) =>
        context.Arguments.TryGetValue(name, out var value) ? Content(facts, value, cancellationToken) : ValueTask.FromResult<Result<JsonElement>>(default(JsonElement));

    /// <summary>An instance fact's content; an undefined element when the instance has none.</summary>
    /// <param name="facts">The fact content port.</param>
    /// <param name="context">The effect's context.</param>
    /// <param name="key">The fact.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, or the error reading it.</returns>
    public static ValueTask<Result<JsonElement>> Fact(IFactContents facts, EffectContext context, string key, CancellationToken cancellationToken) =>
        context.Facts.TryGetValue(FactKey.From(key), out var value) ? Content(facts, value, cancellationToken) : ValueTask.FromResult<Result<JsonElement>>(default(JsonElement));

    /// <summary>The result of an absent argument: an undefined element.</summary>
    public static Result<JsonElement> Undefined { get; } = default(JsonElement);

    /// <summary>The first error among <paramref name="results"/>.</summary>
    /// <param name="results">The reads.</param>
    /// <returns>The first error, or null when every read succeeded.</returns>
    public static Error? FirstError(params Result<JsonElement>[] results) =>
        results.FirstOrDefault(r => r.IsError)?.Error;

    /// <summary>The text a value holds: itself when it is a string, else the first of <paramref name="names"/> its object has as a string.</summary>
    /// <param name="value">The value.</param>
    /// <param name="names">The property names, most wanted first.</param>
    /// <returns>The text, or null.</returns>
    public static string? Text(JsonElement value, params string[] names)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }
        }

        return null;
    }

    /// <summary>The commit shas a push fact carries: the strings (or objects' 'sha') of its 'commits' array.</summary>
    /// <param name="push">The push fact's content.</param>
    /// <returns>The shas; empty when there are none.</returns>
    public static List<string> Commits(JsonElement push)
    {
        var shas = new List<string>();
        if (push.ValueKind != JsonValueKind.Object || !push.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array)
        {
            return shas;
        }

        foreach (var commit in commits.EnumerateArray())
        {
            if (Text(commit, "sha") is { Length: > 0 } sha)
            {
                shas.Add(sha);
            }
        }

        return shas;
    }

    private static async ValueTask<Result<JsonElement>> Content(IFactContents facts, JsonElement value, CancellationToken cancellationToken)
    {
        if (Reference(value) is not { } fact)
        {
            return value;
        }

        var read = await facts.Read(fact, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)   // boundary: a fact that isn't JSON is its text
        {
            using var text = JsonDocument.Parse("\"" + JsonEncodedText.Encode(Encoding.UTF8.GetString(read.Value.Span).Trim()) + "\"");
            return text.RootElement.Clone();
        }
    }

    private static FactValue? Reference(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var head = value.TryGetProperty("headSha", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
        return new FactValue(reference.GetString() ?? string.Empty, sha.GetString() ?? string.Empty, head);
    }
}
