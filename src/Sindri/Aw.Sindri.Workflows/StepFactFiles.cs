using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The facts a Step declares as inputs, shaped as the files its run directory gets before the session starts.</summary>
internal static class StepFactFiles
{
    private const string FactPrefix = "fact:";

    // The facts a lens frame reads from its subject besides its declared inputs (line spec A.3): not descriptor inputs, as the subject picks them. A change lens reads the diff instead.
    private static readonly FactKey[] _designSubject = [FactKey.From("fact:design"), FactKey.From("fact:scope"), FactKey.From("fact:changedSections")];

    /// <summary>The file name to content of each declared read fact that is set.</summary>
    /// <param name="descriptor">The Step's descriptor; null when the catalog doesn't know it (no files).</param>
    /// <param name="facts">The instance's stored facts.</param>
    /// <param name="contents">Reads the stored content.</param>
    /// <param name="scrubber">Removes secrets from the text.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The files; an error when a set fact's content can't be read.</returns>
    internal static ValueTask<Result<IReadOnlyDictionary<string, string>>> Build(BlockDescriptor? descriptor, IReadOnlyDictionary<FactKey, FactValue>? facts, IFactContents contents, ITextScrubber scrubber, CancellationToken cancellationToken) =>
        Build(new FactFileStep(descriptor, Subject: null), facts, contents, scrubber, cancellationToken);

    /// <summary>The file name to content of each declared read fact that is set, and, for a design lens, the design's own facts.</summary>
    /// <param name="step">The Step's descriptor and its 'subject' parameter.</param>
    /// <param name="facts">The instance's stored facts.</param>
    /// <param name="contents">Reads the stored content.</param>
    /// <param name="scrubber">Removes secrets from the text.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The files; an error when a set fact's content can't be read.</returns>
    internal static async ValueTask<Result<IReadOnlyDictionary<string, string>>> Build(FactFileStep step, IReadOnlyDictionary<FactKey, FactValue>? facts, IFactContents contents, ITextScrubber scrubber, CancellationToken cancellationToken)
    {
        var (descriptor, subject) = step;
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        var declared = descriptor?.Inputs ?? [];
        foreach (var key in string.Equals(subject, "design", StringComparison.Ordinal) && descriptor is not null ? declared.Union(_designSubject) : declared)
        {
            if (facts is null || !facts.TryGetValue(key, out var stored))
            {
                continue;   // declared, not set yet: no file
            }

            var bytes = await contents.Read(stored, cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            files[Name(key) + ".json"] = Shaped(scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span)));
        }

        return files;
    }

    private static string Name(FactKey key) => key.Value.StartsWith(FactPrefix, StringComparison.Ordinal) ? key.Value[FactPrefix.Length..] : key.Value;

    /// <summary>What a fact read serves (GetInstanceFactService): stored JSON that is an object or an array as itself, anything else as one JSON string. Never fenced: the file is data for a CLI, not prompt text.</summary>
    private static string Shaped(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is { } node && node is JsonObject or JsonArray)
            {
                return node.ToJsonString();
            }
        }
        catch (JsonException)
        {
            // boundary: stored content that isn't JSON is a text fact
        }

        return JsonValue.Create(text).ToJsonString();
    }
}
