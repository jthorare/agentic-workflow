using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Worktrees;

public sealed class LsofOutputTests
{
    [Fact, Trait("Lane", "unit")]
    public void Fields_group_by_process_and_keep_only_absolute_paths()
    {
        const string output = "p100\ncbash\nn/home/u/repos/aw-wt/feat\nn/dev/pts/3\nn*:8080\np200\ncnode\nn/tmp/x.log\nnTCP localhost:1->2\n";

        var locations = LsofOutput.Parse(output).Select(e => e.Location).ToList();

        Assert.Equal([100, 200], locations.Select(l => l.Pid));
        Assert.Equal(["bash", "node"], locations.Select(l => l.Command), StringComparer.Ordinal);
        Assert.Equal(["/home/u/repos/aw-wt/feat", "/dev/pts/3"], locations[0].Paths);
        Assert.Equal(["/tmp/x.log"], locations[1].Paths);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_R_field_is_the_parent_of_its_process_and_zero_when_lsof_gave_none()
    {
        var entries = LsofOutput.Parse("p100\nR1\ncinit\np200\nR100\ncbash\np300\ncorphan\n");

        Assert.Equal([1, 100, 0], entries.Select(e => e.Ppid));
    }

    [Fact, Trait("Lane", "unit")]
    public void Garbage_and_empty_output_yield_nothing()
    {
        Assert.Empty(LsofOutput.Parse(string.Empty));
        Assert.Empty(LsofOutput.Parse("not lsof\n"));
    }
}
