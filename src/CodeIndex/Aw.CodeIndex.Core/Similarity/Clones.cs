using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>MinHash and LSH over a symbol's tokens, and the Similar decision (spec §4.2 bands, §7). Pure and deterministic: the same tokens give the same bytes on every machine.</summary>
public static class Clones
{
    /// <summary>The MinHash of a token list: <see cref="CodeIndexRules.MinHashLength"/> hashes of the token 3-grams, four bytes each, little endian.</summary>
    /// <param name="tokens">The symbol's tokens.</param>
    /// <returns>The signature, or empty under <see cref="CodeIndexRules.MinCloneTokens"/> tokens (such a symbol has no clone rows).</returns>
    public static byte[] Signature(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < CodeIndexRules.MinCloneTokens)
        {
            return [];
        }

        var mins = new uint[CodeIndexRules.MinHashLength];
        Array.Fill(mins, uint.MaxValue);
        for (var g = 0; g + 2 < tokens.Count; g++)
        {
            var gram = Fnv1a.Hash(string.Join('\u001f', tokens[g], tokens[g + 1], tokens[g + 2]));
            for (var i = 0; i < mins.Length; i++)
            {
                mins[i] = Math.Min(mins[i], Mix(gram, i));
            }
        }

        var signature = new byte[mins.Length * 4];
        for (var i = 0; i < mins.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(signature.AsSpan(i * 4), mins[i]);
        }

        return signature;
    }

    /// <summary>The estimated Jaccard similarity of two signatures: the share of equal hashes.</summary>
    /// <param name="left">One signature.</param>
    /// <param name="right">The other.</param>
    /// <returns>0 to 1; 0 when either is empty or they differ in length.</returns>
    public static double Similarity(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.IsEmpty || left.Length != right.Length)
        {
            return 0;
        }

        var equal = 0;
        for (var i = 0; i + 4 <= left.Length; i += 4)
        {
            if (left.Slice(i, 4).SequenceEqual(right.Slice(i, 4)))
            {
                equal++;
            }
        }

        return equal / (double)(left.Length / 4);
    }

    /// <summary>The LSH band keys of a signature: <see cref="CodeIndexRules.MinHashBands"/> keys, each a band digit and four hashes in hex.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The keys; empty for an empty signature.</returns>
    public static IReadOnlyList<string> BandKeys(ReadOnlySpan<byte> signature)
    {
        if (signature.Length != CodeIndexRules.MinHashLength * 4)
        {
            return [];
        }

        const int rows = CodeIndexRules.MinHashLength / CodeIndexRules.MinHashBands;
        var keys = new List<string>(CodeIndexRules.MinHashBands);
        for (var band = 0; band < CodeIndexRules.MinHashBands; band++)
        {
            var key = new StringBuilder().Append(band.ToString("x", CultureInfo.InvariantCulture));
            for (var row = 0; row < rows; row++)
            {
                var hash = BinaryPrimitives.ReadUInt32LittleEndian(signature[(((band * rows) + row) * 4)..]);
                key.Append(hash.ToString("x8", CultureInfo.InvariantCulture));
            }

            keys.Add(key.ToString());
        }

        return keys;
    }

    /// <summary>Turns a reader's candidates into the answer: exact clones, and near clones at or above the threshold.</summary>
    /// <param name="subject">The symbol asked about; never in its own answer.</param>
    /// <param name="candidates">What the reader found.</param>
    /// <returns>The exact and near clones, at most <see cref="CodeIndexRules.MaxSymbolHits"/> each.</returns>
    public static SimilarClones Find(SymbolRef subject, CloneCandidates candidates)
    {
        var exact = candidates.Exact
            .Where(h => h.Ref != subject)
            .OrderBy(h => h.Ref.Value, StringComparer.Ordinal)
            .Take(CodeIndexRules.MaxSymbolHits)
            .ToList();
        var exactRefs = exact.Select(h => h.Ref).ToHashSet();
        var near = candidates.Banded
            .Where(b => b.Symbol.Ref != subject && !exactRefs.Contains(b.Symbol.Ref))
            .Select(b => new NearClone(b.Symbol, Similarity(candidates.SubjectMinHash.Span, b.MinHash.Span)))
            .Where(n => n.Similarity >= CodeIndexRules.NearCloneThreshold)
            .OrderByDescending(n => n.Similarity)
            .ThenBy(n => n.Symbol.Ref.Value, StringComparer.Ordinal)
            .Take(CodeIndexRules.MaxSymbolHits)
            .ToList();
        return new SimilarClones(exact, near);
    }

    /// <summary>The i-th hash function of a gram (splitmix64 over the gram hash and the function's index), cut to 32 bits.</summary>
    private static uint Mix(ulong gram, int seed)
    {
        var x = gram + ((ulong)(seed + 1) * 0x9E3779B97F4A7C15UL);
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return (uint)((x ^ (x >> 31)) & 0xFFFFFFFFUL);
    }
}
