using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>
/// Reads a fact the way a Step input sees it: the engine hands an input the stored reference ('{ref, sha256, headSha}'), never the content, so the content is read through
/// <see cref="IFactContents"/> and checked against the reference's SHA-256 (content that is not what it says is SND-WF-018, which fails the fill). Content that is not JSON is its text.
/// </summary>
internal static class InputFacts
{
    /// <summary>The fact's content as JSON.</summary>
    /// <param name="contents">The fact content port.</param>
    /// <param name="context">The input's context.</param>
    /// <param name="key">The fact's key, with its 'fact:' prefix.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, null when the instance holds no such fact; an error when the content can't be read or changed.</returns>
    internal static async ValueTask<Result<JsonNode?>> Read(IFactContents contents, StepInputContext context, string key, CancellationToken cancellationToken)
    {
        if (!context.Facts.TryGetValue(FactKey.From(key), out var element))
        {
            return (JsonNode?)null;
        }

        if (Reference(element) is not { } fact)
        {
            return Parse(element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText());
        }

        var read = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var actual = Convert.ToHexStringLower(SHA256.HashData(read.Value.Span));
        if (!string.Equals(actual, fact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowErrors.InputChanged.With($"{fact.Ref} changed: expected sha256 {fact.Sha256[..12]}, read {actual[..12]}");
        }

        return Parse(Encoding.UTF8.GetString(read.Value.Span));
    }

    /// <summary>Whether <paramref name="error"/> must fail the fill (the data changed) rather than become an unavailable section.</summary>
    /// <param name="error">An error from <see cref="Read"/>.</param>
    /// <returns>True for SND-WF-018.</returns>
    internal static bool Fails(Error error) => string.Equals(error.Code, WorkflowErrors.InputChanged.Code, StringComparison.Ordinal);

    /// <summary>A section that says the input has nothing for this Step (the Step still runs).</summary>
    /// <param name="section">The section name.</param>
    /// <param name="text">What to say.</param>
    /// <returns>The sections.</returns>
    internal static InjectionSections Say(string section, string text) => new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = text + "\n" });

    private static Result<JsonNode?> Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: a fact that isn't JSON is its text
        {
            return JsonValue.Create(text.Trim());
        }
    }

    private static FactValue? Reference(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? reference = null, sha = null, head = null;
        foreach (var property in value.EnumerateObject())
        {
            var text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (string.Equals(property.Name, "ref", StringComparison.OrdinalIgnoreCase))
            {
                reference = text;
            }
            else if (string.Equals(property.Name, "sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha = text;
            }
            else if (string.Equals(property.Name, "headSha", StringComparison.OrdinalIgnoreCase))
            {
                head = text;
            }
        }

        return reference is null || sha is null || !reference.StartsWith("fact:", StringComparison.Ordinal) ? null : new FactValue(reference, sha, head);
    }
}
