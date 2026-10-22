using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>What the pull-request effects read of their arguments: a '{ fact }' argument arrives as the stored fact, whose content is read back here.</summary>
internal static class PrEffectFacts
{
    /// <summary>The content of the fact argument <paramref name="name"/>, parsed.</summary>
    /// <param name="context">The effect's context.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="contents">Reads stored facts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, or the error.</returns>
    public static async ValueTask<Result<JsonElement>> Content(EffectContext context, string name, IFactContents contents, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue(name, out var stored))
        {
            return Missing(context, name);
        }

        return await Content(context, stored, name, contents, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The content of the instance's fact <paramref name="key"/>, or null when the instance holds none.</summary>
    /// <param name="context">The effect's context.</param>
    /// <param name="key">The fact's key.</param>
    /// <param name="contents">Reads stored facts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content, null, or the error.</returns>
    public static async ValueTask<Result<JsonElement?>> Held(EffectContext context, string key, IFactContents contents, CancellationToken cancellationToken)
    {
        if (!context.Facts.TryGetValue(Ids(key), out var stored))
        {
            return Results.Success<JsonElement?>(value: null);
        }

        var content = await Content(context, stored, key, contents, cancellationToken).ConfigureAwait(false);
        return content.IsError ? content.Error : Results.Success<JsonElement?>(content.Value);
    }

    /// <summary>The pull request's link: a string, or an object with 'link' or 'url'.</summary>
    /// <param name="fact">The fact's content.</param>
    /// <returns>The link, or null.</returns>
    public static string? Link(JsonElement fact) => fact.ValueKind == JsonValueKind.String ? fact.GetString() : Text(fact, "link") ?? Text(fact, "url");

    /// <summary>The workspace's path: a string, or an object with 'path'.</summary>
    /// <param name="fact">The fact's content.</param>
    /// <returns>The path, or null.</returns>
    public static string? Path(JsonElement fact) => fact.ValueKind == JsonValueKind.String ? fact.GetString() : Text(fact, "path");

    /// <summary>The head sha of 'fact:prState' ('head.sha').</summary>
    /// <param name="state">The fact's content.</param>
    /// <returns>The sha, or null.</returns>
    public static string? Head(JsonElement state) => state.ValueKind == JsonValueKind.Object && state.TryGetProperty("head", out var head) ? Text(head, "sha") : null;

    /// <summary>The sha the checks of 'fact:prState' ran on ('checks.sha').</summary>
    /// <param name="state">The fact's content.</param>
    /// <returns>The sha, or null.</returns>
    public static string? ChecksSha(JsonElement state) => state.ValueKind == JsonValueKind.Object && state.TryGetProperty("checks", out var checks) ? Text(checks, "sha") : null;

    /// <summary>An error naming what the effect lacks.</summary>
    /// <param name="context">The effect's context.</param>
    /// <param name="what">What is missing.</param>
    /// <returns>The error.</returns>
    public static Error Missing(EffectContext context, string what) =>
        WorkflowErrors.EffectFailed.With($"{context.Effect.Value} of {context.Instance} needs {what}, which it does not hold (fix: pass it in the transition's with:)");

    private static async ValueTask<Result<JsonElement>> Content(EffectContext context, JsonElement stored, string name, IFactContents contents, CancellationToken cancellationToken)
    {
        if (stored.ValueKind != JsonValueKind.Object
            || !stored.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !stored.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return Missing(context, name + " as a stored fact");
        }

        var read = await contents.Read(new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null), cancellationToken).ConfigureAwait(false);   // why: both checked to be strings above
        if (read.IsError)
        {
            return read.Error;
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)   // boundary: a fact that isn't JSON names nothing
        {
            return Missing(context, name + " as JSON");
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Aw.Sindri.Core.Workflow.Ids.FactKey Ids(string key) => Aw.Sindri.Core.Workflow.Ids.FactKey.From(key);
}
