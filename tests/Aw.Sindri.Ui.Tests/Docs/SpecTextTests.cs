using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Docs;

/// <summary>The UI spec's wording where the API contract settled it (U3' T14): the lines the screens depend on.</summary>
[Lane(TestLane.Unit)]
public sealed class SpecTextTests
{
    private static readonly string[] _lines = File.ReadAllLines(UiSource.Spec);

    [Fact]
    public void Spec_names_SND_API_011_for_read_your_writes()
    {
        var line = Line("read-your-writes timeouts");
        Assert.Contains("`SND-API-011`", line, StringComparison.Ordinal);
        Assert.DoesNotContain("`SND-API-004`", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_X_picks_the_rejectLike_option()
    {
        var line = Line("| `X` / `Shift+X` |");
        Assert.Contains("the option whose `rejectLike` is true", line, StringComparison.Ordinal);
        Assert.Contains("on a decision", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_recordedOnly_is_served_as_notice()
    {
        var line = Line("| `decision` | The question");
        Assert.Contains("the served `notice`", line, StringComparison.Ordinal);
        Assert.DoesNotContain("as \"recorded;", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_the_failure_row_primary_is_RunService_pending_P6()
    {
        var line = Line("| `failure` | The service");
        Assert.Contains("`RunService`", line, StringComparison.Ordinal);
        Assert.Contains("`pending:P6`", line, StringComparison.Ordinal);
        Assert.Contains("`PauseService`", line, StringComparison.Ordinal);
        Assert.Contains("the served `cli`", line, StringComparison.Ordinal);
    }

    private static string Line(string start)
    {
        var line = _lines.FirstOrDefault(l => l.Contains(start, StringComparison.Ordinal));
        Assert.NotNull(line);
        return line;
    }
}
