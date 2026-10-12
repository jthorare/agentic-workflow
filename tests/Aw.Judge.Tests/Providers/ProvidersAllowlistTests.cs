using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Providers;
using Aw.Judge.Host.Commands;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ProvidersAllowlistTests
{
    [Fact]
    public void Without_the_flag_argv_is_kept_whole() =>
        Assert.Equal(("why d1", null), Joined(ProvidersFlag.Take(["why", "d1"])));

    [Fact]
    public void The_flag_is_taken_out_wherever_it_appears() =>
        Assert.Equal(("ask-check", "jev, claude-cli,jev"), Joined(ProvidersFlag.Take(["ask-check", "--providers", "jev, claude-cli,jev"])));

    [Fact]
    public void A_flag_without_a_value_leaves_no_command_line() =>
        Assert.Null(ProvidersFlag.Take(["wake-gate", "--providers"]).Arguments);

    [Fact]
    public void A_list_is_trimmed_and_deduplicated_in_first_seen_order() =>
        Assert.Equal([ProviderName.Jev, ProviderName.ClaudeCli], ProviderNames.ParseList("jev, claude-cli,jev").Value);

    [Theory]
    [InlineData("", "--providers list is empty")]
    [InlineData(" , ", "--providers list is empty")]
    [InlineData("jev,gpt,Rules", "unknown provider(s): gpt, Rules")]
    public void A_malformed_list_is_an_error_naming_the_problem(string list, string message)
    {
        var parsed = ProviderNames.ParseList(list);

        Assert.True(parsed.IsError);
        Assert.Equal((JudgeErrors.ProvidersFlag.Code, message), (parsed.Error.Code, parsed.Error.Message));
    }

    private static (string? Arguments, string? Value) Joined((string[]? Arguments, string? Value) taken) =>
        (taken.Arguments is null ? null : string.Join(' ', taken.Arguments), taken.Value);
}
