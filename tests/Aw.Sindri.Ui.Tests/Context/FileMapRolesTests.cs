using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view's role lines (issue 1105): per role how many files it matches, and the role-like files no glob covers, read from the map status.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapRolesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FileMapViewModel Files(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    // The status golden with a roles field added for the repo it names.
    private static FakeSindriApi WithRoles(string repo, JsonArray roles, JsonObject? unassigned)
    {
        var status = GoldenFiles.Node("context-map", "map-tabs-anchor");
        var entry = new JsonObject { ["repo"] = repo, ["roles"] = roles };
        if (unassigned is not null)
        {
            entry["unassigned"] = unassigned;
        }

        status["roles"] = new JsonArray(entry);
        return new FakeSindriApi { MapStatusAnswer = new RouteAnswer(HttpStatusCode.OK, status.ToJsonString()) };
    }

    private static string Repo => (string)GoldenFiles.Node("context-map", "map-tabs-anchor")["version"]!["repo"]!;

    [Fact]
    public async Task A_demoted_role_gets_a_line_with_its_file_count()
    {
        var files = Files(WithRoles(Repo, [new JsonObject { ["role"] = "test", ["demoted"] = true, ["files"] = 41 }], unassigned: null));

        await files.Load(path: null, Token);

        Assert.Equal(["test, demoted: 41 files"], files.RoleLines);
        Assert.True(files.HasRoles);
        Assert.False(files.HasUnassigned);
    }

    [Fact]
    public async Task Role_like_files_no_glob_covers_are_counted_and_named()
    {
        var files = Files(WithRoles(
            Repo,
            [new JsonObject { ["role"] = "test", ["demoted"] = true, ["files"] = 41 }],
            new JsonObject { ["count"] = 2, ["examples"] = new JsonArray("src/fixtures/orders.json", "src/fixtures/users.json") }));

        await files.Load(path: null, Token);

        Assert.Equal("2 files look like a role but match no glob", files.UnassignedLine);
        Assert.Equal("src/fixtures/orders.json, src/fixtures/users.json", files.UnassignedExamples);
        Assert.True(files.HasUnassigned);
    }

    [Fact]
    public async Task One_unassigned_file_is_said_in_the_singular()
    {
        var files = Files(WithRoles(Repo, [], new JsonObject { ["count"] = 1, ["examples"] = new JsonArray("a/fixtures/x.json") }));

        await files.Load(path: null, Token);

        Assert.Equal("1 file looks like a role but matches no glob", files.UnassignedLine);
        Assert.Empty(files.RoleLines);
    }

    [Fact]
    public async Task A_status_without_roles_shows_nothing()
    {
        var files = Files(new FakeSindriApi());

        await files.Load(path: null, Token);

        Assert.Empty(files.RoleLines);
        Assert.Equal((false, string.Empty, string.Empty), (files.HasRoles, files.UnassignedLine, files.UnassignedExamples));
    }

    [Fact]
    public async Task Roles_of_another_repo_are_not_shown()
    {
        var files = Files(WithRoles("some-other-repo", [new JsonObject { ["role"] = "test", ["demoted"] = true, ["files"] = 9 }], unassigned: null));

        await files.Load(path: null, Token);

        Assert.False(files.HasRoles);
    }
}
