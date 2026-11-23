using Aw.Meta.Host.Commands;

namespace Aw.Meta.Tests.Cli;

public sealed class RepeatedOptionTests
{
    private static readonly CommandSpec _spec = DelegateCommand.Entry.Spec;

    [Fact, Trait("Lane", "unit")]
    public void A_repeated_label_keeps_every_value_in_order()
    {
        var line = CommandLine.Parse(["--lane", "api", "--label", "a", "--title", "t", "--label", "b"], _spec, out var problem);

        Assert.Equal(string.Empty, problem);
        Assert.Equal(["a", "b"], Assert.IsType<CommandLine>(line).All("label"));
        Assert.Equal("api", line["lane"]);
        Assert.Empty(line.All("other"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_without_a_value_is_a_usage_error()
    {
        Assert.Null(CommandLine.Parse(["--label", "--lane", "api"], _spec, out var problem));
        Assert.Equal("--label needs one non-empty value", problem);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_plain_option_given_twice_is_still_refused()
    {
        Assert.Null(CommandLine.Parse(["--lane", "api", "--lane", "ui"], _spec, out var problem));
        Assert.Equal("--lane needs one non-empty value", problem);
    }
}
