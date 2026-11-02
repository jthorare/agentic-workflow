using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri index embed [pause|resume]' (owned-index spec §5.6) is the 'services' command on the 'index-embed' service.</summary>
public sealed class IndexEmbedArgumentsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(new string[0], new[] { "run", "index-embed" })]
    [InlineData(new[] { "--detach", "--json" }, new[] { "run", "index-embed", "--detach", "--json" })]
    [InlineData(new[] { "pause" }, new[] { "pause", "index-embed" })]
    [InlineData(new[] { "resume", "--no-start" }, new[] { "resume", "index-embed", "--no-start" })]
    public void The_verb_becomes_the_services_command(string[] rest, string[] expected)
    {
        var parsed = IndexEmbedArguments.Services(rest);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(expected, parsed.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_word_is_invalid_and_names_the_usage()
    {
        var parsed = IndexEmbedArguments.Services(["stop"]);

        Assert.True(parsed.IsError);
        Assert.Contains("index embed pause|resume", parsed.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "index", "embed" }, true)]
    [InlineData(new[] { "index", "embed", "--json" }, true)]
    [InlineData(new[] { "index", "embed", "pause" }, false)]
    [InlineData(new[] { "index", "embed", "resume" }, false)]
    public void Only_the_running_form_follows_a_job(string[] args, bool follows) =>
        Assert.Equal(follows, Interrupt.FollowsJob(args));

    [Fact, Trait("Lane", "unit")]
    public void Detach_does_not_follow_the_job() =>
        Assert.False(Interrupt.FollowsJob(["index", "embed", "--detach"]));
}
