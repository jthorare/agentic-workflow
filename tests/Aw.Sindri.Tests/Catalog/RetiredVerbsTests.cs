using Aw.Kernel.Testing;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>Issue 2258 PR 3: the verbs of the removed recipe engine and factory line are not in the catalog.</summary>
[Lane(TestLane.Unit)]
public sealed class RetiredVerbsTests
{
    [Fact]
    public void The_command_list_has_no_recipe_scoping_line_or_docs_check_verbs()
    {
        var retired = new[] { "sindri recipe ", "sindri scoping ", "sindri line ", "sindri docs check" };

        var found = new CommandCatalog().Rows.Select(static row => row.Cli.Text)
            .Where(text => retired.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal))).ToList();

        Assert.Empty(found);
    }
}
