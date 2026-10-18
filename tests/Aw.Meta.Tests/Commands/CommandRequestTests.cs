using Aw.Meta.Core.Commands;

namespace Aw.Meta.Tests.Commands;

public sealed class CommandRequestTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_tail_of_less_than_one_character_is_refused(int tail) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CommandRequest(["x"], _timeout) { TailCharacters = tail });

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(CommandRequest.DefaultTailCharacters)]
    [InlineData(int.MaxValue)]
    public void A_tail_of_one_or_more_characters_is_kept(int tail) =>
        Assert.Equal(tail, new CommandRequest(["x"], _timeout) { TailCharacters = tail }.TailCharacters);

    [Fact, Trait("Lane", "unit")]
    public void The_default_tail_is_64_KB() => Assert.Equal(64 * 1024, new CommandRequest(["x"], _timeout).TailCharacters);
}
