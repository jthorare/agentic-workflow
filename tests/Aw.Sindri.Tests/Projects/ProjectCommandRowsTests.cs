using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The four project writes' catalog rows (projects page spec §5.3): desk class, one POST route each. A session is refused by the services (ProjectSessionRefusalTests): the desk class itself lets one through.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectCommandRowsTests
{
    private static readonly IReadOnlyList<CommandRow> _rows = [.. new CommandCatalog().Rows.Where(static r => r.Route is { } route && route.StartsWith("/sindri/projects", StringComparison.Ordinal))];

    [Fact]
    public void Each_write_has_a_desk_row_with_its_route()
    {
        (string, Type?, string?)[] expected =
            [("AttachProject", typeof(AttachProjectRequest), "/sindri/projects"), ("ChangeProject", typeof(ChangeProjectRequest), "/sindri/projects/{projectId}/change"),
                ("DetachProject", typeof(DetachProjectRequest), "/sindri/projects/{projectId}/detach"), ("HideProject", typeof(HideProjectRequest), "/sindri/projects/hide")];
        var actual = _rows.OrderBy(static r => r.Action.Value, StringComparer.Ordinal).Select(static r => (r.Action.Value, r.CommandType, r.Route)).ToArray();
        Assert.Equal(expected, actual);
        Assert.All(_rows, static r => Assert.Equal((ActionClass.Desk, "POST"), (r.Class, r.Method)));
    }

    [Fact]
    public void A_human_may_run_each_write()
    {
        Assert.All(_rows, static row =>
        {
            Assert.True(Table.Decide(row.Class, A.Caller().AtTerminal().Build(), row).Allowed);
            Assert.True(Table.Decide(row.Class, A.Caller().Human().Build(), row).Allowed);
        });
    }
}
