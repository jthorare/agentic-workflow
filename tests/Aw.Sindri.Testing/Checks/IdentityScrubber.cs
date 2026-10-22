using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Testing.Checks;

/// <summary>An <see cref="ITextScrubber"/> that changes nothing, for a test whose subject is not the scrubbing.</summary>
public sealed class IdentityScrubber : ITextScrubber
{
    /// <inheritdoc/>
    public string Scrub(string text) => text;
}
