using System.Security.Cryptography;
using System.Text;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// The keys of cached verdicts. A source's hash covers its path and text, with each hunk header's line numbers left out
/// (lines added above a hunk shift them without changing what is judged). A verdict's key covers the cache version, the
/// model, the trait file's bytes, the question Jev is asked (so a reworded question is a miss) and the source's hash.
/// The mode and the line numbers Jev is shown are left out: they don't decide the answer.
/// </summary>
internal sealed class TraitVerdictKeys
{
    /// <summary>Bump to drop every cached verdict, for a change the key can't see.</summary>
    internal const string Version = "trait-verdict-1";

    private readonly Dictionary<Trait, string> _traits = [];

    /// <summary>The SHA-256 of the source, lowercase hex.</summary>
    /// <param name="source">The excerpt.</param>
    /// <returns>The hash.</returns>
    internal static string SourceHash(DiffSource source)
    {
        var text = new StringBuilder(source.Path).Append('\0');
        foreach (var line in source.Text.Split('\n'))
        {
            _ = text.Append(line.StartsWith("@@", StringComparison.Ordinal) ? "@@" : line).Append('\n');
        }

        return Hash(text.ToString());
    }

    /// <summary>The key of one trait's verdict on a source.</summary>
    /// <param name="trait">The trait.</param>
    /// <param name="sourceHash">The source's <see cref="SourceHash"/>.</param>
    /// <returns>The key.</returns>
    internal string Key(Trait trait, string sourceHash)
    {
        if (!_traits.TryGetValue(trait, out var traitHash))
        {
            var question = TraitQuestions.For(trait);
            var text = new StringBuilder(trait.FileHash).Append('\0').Append(question.Prompt);
            foreach (var (option, meaning) in question.Criteria)
            {
                _ = text.Append('\0').Append(option).Append('=').Append(meaning);
            }

            traitHash = _traits[trait] = Hash(text.ToString());
        }

        return Hash(string.Join('\0', Version, JevModel.Id, traitHash, sourceHash));
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
