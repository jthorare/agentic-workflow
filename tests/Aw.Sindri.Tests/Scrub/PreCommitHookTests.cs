using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.Scrub;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>The pre-commit hook body and the check of a file already there (cutover spec §6.2 item 3).</summary>
[Lane(TestLane.Unit)]
public sealed class PreCommitHookTests
{
    [Fact]
    public void The_v3_body_has_the_marker_the_baked_in_path_and_no_shape_line()
    {
        var body = PreCommitHook.Body("/home/thor/.local/bin/sindri");

        Assert.StartsWith("#!/bin/sh\n", body, StringComparison.Ordinal);
        Assert.Contains("\n# sindri-pre-commit v3\n", body, StringComparison.Ordinal);
        Assert.Contains("SINDRI='/home/thor/.local/bin/sindri'", body, StringComparison.Ordinal);
        Assert.Contains("\"$SINDRI\" scrub --staged || exit 1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("shape", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_v3_body_fails_closed_when_the_binary_is_missing_and_names_setup_sh()
    {
        var body = PreCommitHook.Body("/home/thor/.local/bin/sindri");

        Assert.Contains("sindri-scrub: ", body, StringComparison.Ordinal);
        Assert.Contains("not found", body, StringComparison.Ordinal);
        Assert.Contains("exit 1", body, StringComparison.Ordinal);
        Assert.Contains("./setup.sh", body, StringComparison.Ordinal);
        Assert.DoesNotContain("install-sindri.sh", body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_with_a_quote_is_baked_in_as_one_shell_word()
    {
        var body = PreCommitHook.Body("/home/o'brien/.local/bin/sindri");

        Assert.Contains("SINDRI='/home/o'\\''brien/.local/bin/sindri'", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, HookKind.None)]
    [InlineData("#!/bin/sh\n# sindri-pre-commit v3\n", HookKind.V3)]
    [InlineData(ScrubFixtures.V2Hook, HookKind.V2)]
    [InlineData(ScrubFixtures.V1Hook, HookKind.V1)]
    [InlineData(ScrubFixtures.ForeignHook, HookKind.Foreign)]
    [InlineData(ScrubFixtures.MentionsMarker, HookKind.Foreign)]
    public void An_existing_file_is_classified_by_whole_line_markers(string? existing, HookKind expected) =>
        Assert.Equal(expected, PreCommitHook.Classify(existing));
}
