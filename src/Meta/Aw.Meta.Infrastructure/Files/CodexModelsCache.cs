using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>ICodexModels over ~/.codex/models_cache.json: the "slug" of each entry of "models".</summary>
/// <param name="path">The cache file.</param>
internal sealed class CodexModelsCache(string path) : ICodexModels
{
    public async ValueTask<Result<IReadOnlySet<string>?>> Known(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Results.Success<IReadOnlySet<string>?>(value: null);
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            {
                return Results.Success<IReadOnlySet<string>?>(value: null);
            }

            var slugs = models.EnumerateArray()
                .Select(static m => m.ValueKind == JsonValueKind.Object && m.TryGetProperty("slug", out var slug) && slug.ValueKind == JsonValueKind.String ? slug.GetString() : null)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            return Results.Success<IReadOnlySet<string>?>(slugs);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Results.Success<IReadOnlySet<string>?>(value: null);   // advisory: an unreadable cache checks nothing
        }
    }
}
