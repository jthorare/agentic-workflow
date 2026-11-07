using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Skills;

/// <summary>'--skills &lt;dir&gt;' on 'recipe validate|eval' (skill flows spec §3.2): the flag leaves the arguments and the folder reaches the composition as AW_SINDRI_SKILLS_DIR.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillsFlagTests
{
    private static string? Nothing(string name) => name == "HOME" ? "/home/u" : null;

    [Fact]
    public void The_flag_is_taken_out_and_the_folder_is_the_environments()
    {
        var applied = SkillsFlag.Apply(["recipe", "validate", "r.yaml", "--skills", "skills", "--json"], Nothing, "/work/repo").Value;

        Assert.Equal(["recipe", "validate", "r.yaml", "--json"], applied.Args);
        Assert.Equal("/work/repo/skills", applied.Environment("AW_SINDRI_SKILLS_DIR"));
        Assert.Equal("/home/u", applied.Environment("HOME"));
    }

    [Fact]
    public void Eval_takes_it_too_and_an_absolute_folder_stays_as_given()
    {
        var applied = SkillsFlag.Apply(["recipe", "eval", "--shipped", "--skills", "/opt/skills"], Nothing, "/work/repo").Value;

        Assert.Equal(["recipe", "eval", "--shipped"], applied.Args);
        Assert.Equal("/opt/skills", applied.Environment("AW_SINDRI_SKILLS_DIR"));
    }

    [Theory]
    [InlineData("recipe", "list", "--skills", "x")]
    [InlineData("skill", "run", "review", "--skills", "x")]
    [InlineData("recipe", "validate", "r.yaml")]
    public void Any_other_command_line_is_left_alone(params string[] args)
    {
        var applied = SkillsFlag.Apply(args, Nothing, "/work/repo").Value;

        Assert.Equal(args, applied.Args);
        Assert.Null(applied.Environment("AW_SINDRI_SKILLS_DIR"));
    }

    [Theory]
    [InlineData("recipe", "validate", "r.yaml", "--skills")]
    [InlineData("recipe", "eval", "r.yaml", "--skills", "--json")]
    public void A_flag_with_no_folder_is_a_usage_error(params string[] args) => Assert.True(SkillsFlag.Apply(args, Nothing, "/work/repo").IsError);
}
