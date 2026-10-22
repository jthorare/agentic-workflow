using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Similarity;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Similarity;

public sealed class ClonesTests
{
    private static List<string> Body(int count, string prefix = "t") => [.. Enumerable.Range(0, count).Select(i => $"{prefix}{i.ToString(CultureInfo.InvariantCulture)}")];

    [Fact, Trait("Lane", "unit")]
    public void Fnv1a_matches_the_published_vectors()
    {
        Assert.Equal(0xcbf29ce484222325UL, Fnv1a.Hash(""));
        Assert.Equal(0xaf63dc4c8601ec8cUL, Fnv1a.Hash("a"));
        Assert.Equal(0x85944171f73967e8UL, Fnv1a.Hash("foobar"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Signature_is_empty_under_30_tokens_and_256_bytes_from_30()
    {
        Assert.Empty(Clones.Signature(Body(CodeIndexRules.MinCloneTokens - 1)));
        Assert.Equal(CodeIndexRules.MinHashLength * 4, Clones.Signature(Body(CodeIndexRules.MinCloneTokens)).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Signature_is_a_known_vector()
    {
        var signature = Clones.Signature(Body(40));

        Assert.Equal("322c1e05d77f3e06d54e360720e695022ee47b0422cf9401960a5f057b22b404", Convert.ToHexStringLower(signature, 0, 32));
        Assert.Equal(signature, Clones.Signature(Body(40)));
        Assert.NotEqual(signature, Clones.Signature(Body(40, "u")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Similarity_is_the_share_of_equal_hashes()
    {
        var a = Clones.Signature(Body(40));
        var near = Clones.Signature([.. Body(40).Skip(1), "extra"]);

        Assert.Equal(1.0, Clones.Similarity(a, a));
        Assert.InRange(Clones.Similarity(a, near), 0.8, 0.99);
        Assert.InRange(Clones.Similarity(a, Clones.Signature(Body(40, "z"))), 0, 0.1);
        Assert.Equal(0, Clones.Similarity(a, []));
        Assert.Equal(0, Clones.Similarity(a, a.AsSpan(0, 8)));
    }

    [Fact, Trait("Lane", "unit")]
    public void BandKeys_are_sixteen_keys_that_two_equal_signatures_share()
    {
        var a = Clones.Signature(Body(40));
        var keys = Clones.BandKeys(a);

        Assert.Equal(CodeIndexRules.MinHashBands, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.Equal(33, k.Length));
        Assert.Equal(keys, Clones.BandKeys(Clones.Signature(Body(40))));
        Assert.Empty(Clones.BandKeys([]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_near_duplicate_pair_shares_a_band_and_clears_the_threshold()
    {
        var original = Tokens("int Total(Order o) { var sum = 0; foreach (var line in o.Lines) { if (line.Qty > 0) { sum += line.Price * line.Qty; } else { log.Skip(line.Id); } } return sum; }");
        var edited = Tokens("int Total(Order o) { var sum = 0; foreach (var line in o.Lines) { if (line.Qty > 0) { sum += line.Price * line.Qty; } else { log.Skip(line.Id); } } return sum + o.Fee; }");

        var shared = Clones.BandKeys(Clones.Signature(original)).Intersect(Clones.BandKeys(Clones.Signature(edited)), StringComparer.Ordinal);

        Assert.NotEmpty(shared);
        Assert.True(Clones.Similarity(Clones.Signature(original), Clones.Signature(edited)) >= CodeIndexRules.NearCloneThreshold);
    }

    private static IReadOnlyList<string> Tokens(string text) => CloneTokenizer.Tokenize(text);

    private static SymbolHit Hit(string name) => new(SymbolRef.From($"r:src/A.cs#{name}"), SymbolKind.Method, name, "src/A.cs", 1, 9, name + "()", Exported: true);

    private static MinHashCandidate Banded(string name, byte[] signature) => new(Hit(name), signature);

    [Fact, Trait("Lane", "unit")]
    public void Find_leaves_the_subject_out_and_orders_exact_clones_by_ref()
    {
        var found = Clones.Find(Hit("Subject").Ref, new CloneCandidates([Hit("Zed"), Hit("Subject"), Hit("Alpha")], default, []));

        Assert.Equal(["Alpha", "Zed"], found.Exact.Select(h => h.Name), StringComparer.Ordinal);
        Assert.Empty(found.Near);
    }

    [Fact, Trait("Lane", "unit")]
    public void Find_keeps_banded_candidates_at_or_above_the_threshold_closest_first()
    {
        var subject = Clones.Signature(Body(40));
        var close = Clones.Signature([.. Body(40).Skip(1), "extra"]);
        var far = Clones.Signature(Body(40, "z"));

        var found = Clones.Find(Hit("Subject").Ref, new CloneCandidates([Hit("Twin")], subject,
            [Banded("Far", far), Banded("Close", close), Banded("Twin", subject), Banded("Subject", subject), Banded("Same", subject)]));

        Assert.Equal(["Same", "Close"], found.Near.Select(n => n.Symbol.Name), StringComparer.Ordinal);
        Assert.Equal(1.0, found.Near[0].Similarity);
        Assert.True(found.Near[1].Similarity >= CodeIndexRules.NearCloneThreshold);
    }

    [Fact, Trait("Lane", "unit")]
    public void Find_gives_no_near_clones_when_the_subject_has_no_signature()
        => Assert.Empty(Clones.Find(Hit("Subject").Ref, new CloneCandidates([], default, [Banded("Other", Clones.Signature(Body(40)))])).Near);

    [Fact, Trait("Lane", "unit")]
    public void Find_caps_each_list_at_the_hit_limit()
    {
        var exact = Enumerable.Range(0, CodeIndexRules.MaxSymbolHits + 20).Select(i => Hit($"E{i.ToString("D4", CultureInfo.InvariantCulture)}")).ToList();

        Assert.Equal(CodeIndexRules.MaxSymbolHits, Clones.Find(Hit("Subject").Ref, new CloneCandidates(exact, default, [])).Exact.Count);
    }
}
