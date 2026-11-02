using System.Globalization;
using Aw.Context.Core.Chunking;

namespace Aw.Context.Tests.Chunking;

public sealed class ChunkerTests
{
    /// <summary>Ten words a line, each word eight characters ("word0001"), so a window holds about 170 words.</summary>
    private static string Prose(int words) => string.Join('\n', Enumerable.Range(0, (words + 9) / 10).Select(line =>
        string.Join(' ', Enumerable.Range(line * 10, Math.Min(10, words - (line * 10))).Select(w => "word" + w.ToString("D4", CultureInfo.InvariantCulture)))));

    private static string[] Words(string text) => text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   \n\t \r\n ")]
    public void Empty_or_whitespace_text_has_no_chunks(string text) => Assert.Empty(Chunker.Split(text));

    [Fact, Trait("Lane", "unit")]
    public void Short_text_is_one_chunk_with_its_line_span_and_token_estimate()
    {
        var chunks = Chunker.Split("alpha beta\n\ngamma delta epsilon\nzeta");

        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.Seq);
        Assert.Equal("alpha beta\n\ngamma delta epsilon\nzeta", chunk.Text);
        Assert.Equal(1, chunk.LineStart);
        Assert.Equal(4, chunk.LineEnd);
        Assert.Equal(Chunker.Estimate(words: 6, chars: chunk.Text.Length), chunk.Tokens);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_estimate_is_the_larger_of_words_times_1_3_and_characters_over_3_5_rounded_up()
    {
        Assert.Equal(13, Chunker.Estimate(words: 10, chars: 10));   // 13.0 beats 2.86
        Assert.Equal(15, Chunker.Estimate(words: 1, chars: 51));    // 14.57 beats 1.3
        Assert.Equal(2, Chunker.Estimate(words: 1, chars: 1));      // 1.3 rounds up
    }

    [Fact, Trait("Lane", "unit")]
    public void Crlf_lines_count_once()
    {
        var chunk = Assert.Single(Chunker.Split("one\r\ntwo\r\nthree"));

        Assert.Equal(3, chunk.LineEnd);
    }

    [Fact, Trait("Lane", "unit")]
    public void Long_prose_is_windowed_at_400_tokens_with_contiguous_seq()
    {
        var chunks = Chunker.Split(Prose(3_000));

        Assert.True(chunks.Count > 5);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Seq));
        Assert.All(chunks, c => Assert.InRange(c.Tokens, 1, 400));
        Assert.All(chunks, c => Assert.Equal(Chunker.Estimate(Words(c.Text).Length, c.Text.Length), c.Tokens));
        Assert.All(chunks.SkipLast(1), c => Assert.True(c.Tokens > 380, "a full window is filled to near the limit"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Consecutive_windows_share_about_40_tokens_of_overlap()
    {
        var chunks = Chunker.Split(Prose(3_000));

        for (var i = 1; i < chunks.Count; i++)
        {
            var before = Words(chunks[i - 1].Text);
            var after = Words(chunks[i].Text);
            var shared = Enumerable.Range(1, Math.Min(before.Length, after.Length)).Last(k => before.TakeLast(k).SequenceEqual(after.Take(k), StringComparer.Ordinal));
            var text = string.Join(' ', after.Take(shared));
            var tokens = Chunker.Estimate(shared, text.Length);
            Assert.InRange(tokens, 30, 40);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Windows_cover_the_whole_text_and_their_line_spans_run_forward_without_gaps()
    {
        var text = Prose(3_000);
        var chunks = Chunker.Split(text);

        Assert.Equal(1, chunks[0].LineStart);
        Assert.Equal(300, chunks[^1].LineEnd);
        Assert.Equal(Words(text)[^1], Words(chunks[^1].Text)[^1], StringComparer.Ordinal);
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].LineStart >= chunks[i - 1].LineStart);
            Assert.True(chunks[i].LineEnd > chunks[i - 1].LineEnd);
            Assert.True(chunks[i].LineStart <= chunks[i - 1].LineEnd + 1);
        }

        Assert.All(chunks, c => Assert.True(c.LineStart <= c.LineEnd));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_long_run_without_spaces_is_split_so_every_window_fits()
    {
        var run = string.Concat(Enumerable.Range(0, 10_000).Select(i => (char)('A' + (i * 7 % 26))));

        var chunks = Chunker.Split(run);

        Assert.True(chunks.Count > 7);
        Assert.All(chunks, c => Assert.InRange(c.Tokens, 1, 400));
        Assert.All(chunks, c => Assert.InRange(Chunker.Estimate(1, c.Text.Length), 1, 400));
        Assert.Contains(run[^50..], chunks[^1].Text, StringComparison.Ordinal);
        Assert.All(chunks, c => Assert.DoesNotContain(' ', c.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_text_gives_the_same_chunks()
    {
        var text = Prose(1_500);

        Assert.Equal(Chunker.Split(text), Chunker.Split(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_chunk_key_is_the_record_key_hash_and_the_sequence_number() =>
        Assert.Equal("repo:x/a#b.md#3", Chunker.Key("repo:x/a#b.md", 3));
}
