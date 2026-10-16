using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.DbRun;

/// <summary>A scrubber that also treats some JSON keys as secret: the keys a result gave to repeats of a secret-named column (cookie_2 for a second cookie).</summary>
internal sealed class SecretKeysScrubber(ITextScrubber inner, IReadOnlySet<string> keys) : ITextScrubber
{
    /// <inheritdoc/>
    public bool IsSecretName(string name) => keys.Contains(name) || inner.IsSecretName(name);

    /// <inheritdoc/>
    public string Scrub(string text) => inner.Scrub(text);
}
