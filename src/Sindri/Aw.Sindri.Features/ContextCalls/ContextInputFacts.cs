using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Turns a '{ fact: … }' argument into the fact's content. The engine hands a Step input the stored reference ('{ref, sha256, headSha}', the
/// serialized <see cref="FactValue"/>), never the content; so an input that wants the value (anchors, a window) reads it here through the same
/// <see cref="IFactContents"/> port the effects use, checks it against the reference's SHA-256, and parses it as JSON (a content that isn't JSON is its text).
/// A '{ profile: key }' argument is resolved here too, against the approved profile snapshot (never in workflow code, which must replay): a key the
/// snapshot can't give is an error: a key no input can read is SND-WF-021 (see <see cref="Fails"/>, it fails the fill), and no approved profile is SND-PROFILE-005 (an unavailable section, like an unreadable fact). A key the profile leaves unset is the open window.
/// A reference that can't be read is an error that names the argument and the fact (an honest unavailable section); content that isn't what its SHA-256 says is SND-WF-018 (see <see cref="Fails"/>), which fails the fill.
/// </summary>
/// <param name="contents">The fact content port.</param>
/// <param name="profile">The approved profile, for '{ profile: key }' arguments; without it every such argument is SND-PROFILE-005.</param>
internal sealed class ContextInputFacts(IFactContents contents, IApprovedProfile? profile = null)
{
    /// <summary>The context with every fact-reference argument replaced by the fact's content.</summary>
    /// <param name="context">The input's context.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The resolved context (the same one when no argument is a reference), or the first reference that could not be used.</returns>
    internal async ValueTask<Result<StepInputContext>> Resolve(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        Dictionary<string, JsonElement>? resolved = null;
        foreach (var (name, value) in context.Arguments)
        {
            if (ProfileKey(value) is { } key)
            {
                var setting = await ProfileValue(name, key, cancellationToken).ConfigureAwait(false);
                if (setting.IsError)
                {
                    return setting.Error;
                }

                resolved ??= new Dictionary<string, JsonElement>(context.Arguments, StringComparer.Ordinal);
                resolved[name] = setting.Value;
                continue;
            }

            if (Reference(value) is not { } fact)
            {
                continue;
            }

            var content = await Content(name, fact, cancellationToken).ConfigureAwait(false);
            if (content.IsError)
            {
                return content.Error;
            }

            resolved ??= new Dictionary<string, JsonElement>(context.Arguments, StringComparer.Ordinal);
            resolved[name] = content.Value;
        }

        return resolved is null ? context : context with { Arguments = resolved };
    }

    /// <summary>Whether <paramref name="error"/> must fail the fill (the data changed) rather than become an unavailable section.</summary>
    /// <param name="error">An error from <see cref="Resolve"/>.</param>
    /// <returns>True for SND-WF-018 and SND-WF-021.</returns>
    internal static bool Fails(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, WorkflowErrors.InputChanged.Code, StringComparison.Ordinal)
            || string.Equals(error.Code, WorkflowErrors.ProfileKeyMissing.Code, StringComparison.Ordinal);
    }

    private async ValueTask<Result<JsonElement>> ProfileValue(string name, string key, CancellationToken cancellationToken)
    {
        if (profile is null)
        {
            return SindriErrors.ProfileNotApproved.With($"the {name} argument names profile key {key}, and this input has no profile to read it from");
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (approved.Value is not { } loaded)
        {
            return SindriErrors.ProfileNotApproved.With($"the {name} argument names profile key {key}, and no profile is approved (fix: sindri profile approve)");
        }

        return Setting(loaded.Profile, key) is { } setting
            ? setting
            : WorkflowErrors.ProfileKeyMissing.With($"the {name} argument names profile key {key}, which is not a key a Step input can read");
    }

    /// <summary>The value of a profile key a Step input may name, or null when the key is not one a Step input can read. A key the profile leaves unset is the open window.</summary>
    private static JsonElement? Setting(SindriProfile profile, string key)
    {
        if (!string.Equals(key, "workflow.scoping.historyWindow", StringComparison.Ordinal))
        {
            return null;
        }

        var since = profile.Workflow?.Scoping?.HistoryWindow?.Since;
        var before = profile.Workflow?.Scoping?.HistoryWindow?.Before;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (since is not null)
            {
                writer.WriteString("since", since);
            }

            if (before is not null)
            {
                writer.WriteString("before", before);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static string? ProfileKey(JsonElement value) =>
        value is { ValueKind: JsonValueKind.Object } && value.GetPropertyCount() == 1 && value.TryGetProperty("profile", out var key) && key.ValueKind == JsonValueKind.String
            ? key.GetString()
            : null;

    private async ValueTask<Result<JsonElement>> Content(string name, FactValue fact, CancellationToken cancellationToken)
    {
        var read = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextErrors.QueryInvalid.With($"the {name} argument names {fact.Ref}, which can't be read");
        }

        var actual = Convert.ToHexStringLower(SHA256.HashData(read.Value.Span));
        if (!string.Equals(actual, fact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowErrors.InputChanged.With($"{fact.Ref} changed: expected sha256 {fact.Sha256[..12]}, read {actual[..12]}");
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)   // boundary: a fact that isn't JSON is its text
        {
            using var text = JsonDocument.Parse("\"" + JsonEncodedText.Encode(Encoding.UTF8.GetString(read.Value.Span).Trim()).ToString() + "\"");
            return text.RootElement.Clone();
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

        if (reference is null || sha is null || !reference.StartsWith("fact:", StringComparison.Ordinal))
        {
            return null;
        }

        return new FactValue(reference, sha, head);
    }
}
