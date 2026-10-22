using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>Where the embeddings layer may send text (spec §5.6): an IP-literal loopback address, never 'localhost', and never a cloud model.</summary>
public static class EmbeddingTarget
{
    /// <summary>Checks the URL of the settings.</summary>
    /// <param name="url">The embedding server's URL.</param>
    /// <returns>True for an http URL on 127.0.0.1 or [::1] with no credentials.</returns>
    public static bool IsLoopbackUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            && uri.UserInfo.Length == 0
            && (string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal) || string.Equals(uri.Host, "[::1]", StringComparison.Ordinal));
    }

    /// <summary>Checks the URL and the model of the settings.</summary>
    /// <param name="settings">The embeddings settings.</param>
    /// <returns>SND-CODEIDX-005 when the URL is not loopback or the model is a cloud model; otherwise null.</returns>
    public static Error? Check(EmbeddingsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsLoopbackUrl(settings.Url))
        {
            return CodeIndexErrors.EmbeddingTargetRefused.With("the embedding URL must be http://127.0.0.1 or http://[::1], with no credentials");
        }

        return settings.Model is { } model && (model.Length == 0 || model.Contains("cloud", StringComparison.OrdinalIgnoreCase))
            ? CodeIndexErrors.EmbeddingTargetRefused.With("the embedding model must be a local model, never a cloud model")
            : null;
    }
}
