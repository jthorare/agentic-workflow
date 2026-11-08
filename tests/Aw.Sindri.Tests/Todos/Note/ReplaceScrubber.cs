using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>A scrubber that swaps one secret for a mask, the way the real one does for a token (REQ-W2).</summary>
internal sealed class ReplaceScrubber(string secret, string mask) : ITextScrubber
{
    public string Scrub(string text) => text.Replace(secret, mask, StringComparison.Ordinal);
}
