using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>Settings -> Library's goldens as the app reads them: the roots of GET /sindri/library, each write's answer, and the refusals' field member.</summary>
[Lane(TestLane.Unit)]
public sealed class LibraryRootsGoldenTests
{
    private static LibraryView Library(string scenario) =>
        Kernel.Json.AwJson.Deserialize<LibraryView>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("library", scenario)), SindriApiClient.Json).Value;

    [Fact]
    public void Every_library_golden_carries_roots_and_the_session_form_has_none()
    {
        foreach (var scenario in (string[])["ok", "with-errors", "near-misses-only", "empty", "capped", "root-unreadable"])
        {
            Assert.NotEmpty(Library(scenario).Roots);
        }

        Assert.Empty(Library("session").Roots);
        Assert.True(JsonNode.Parse(GoldenFiles.Api("library", "session"))!.AsObject().ContainsKey("roots"));   // why: required, present even when empty
    }

    [Fact]
    public void The_roots_are_ordered_aw_then_vaults_then_repo_roots_and_carry_every_field()
    {
        var roots = Library("ok").Roots;

        Assert.Equal(["aw", "vault", "vault", "repo", "repo"], roots.Select(r => r.Kind), StringComparer.Ordinal);
        var work = roots[1];
        Assert.Equal(("work", "~/Documents/WorkVault", null, true, 20, 0, null), (work.Name, work.Path, work.Scope, work.On, work.Docs, work.Errors, work.Unreadable));
        Assert.Equal(["Private/**"], work.Deny, StringComparer.Ordinal);
        Assert.Equal(DateTimeOffset.Parse("2025-12-31T23:57:00Z", System.Globalization.CultureInfo.InvariantCulture), work.ScannedAt);
        Assert.Null(roots[2].ScannedAt);   // the not-scanned vault
        Assert.Equal("jthorare/agentic-workflow · docs/**", roots[3].Scope);
    }

    [Fact]
    public void An_unreadable_vault_has_a_reason_and_a_turned_off_aw_is_not_on()
    {
        Assert.Equal("permission denied", Library("root-unreadable").Roots.Single(r => r.Unreadable is not null).Unreadable);
        Assert.False(Library("empty").Roots[0].On);
    }

    [Theory]
    [InlineData("added", true, 0)]
    [InlineData("added-pending-others", false, 2)]
    [InlineData("removed", true, 0)]
    [InlineData("aw-off", true, 0)]
    public void Each_write_answers_approved_and_how_many_changes_still_wait(string scenario, bool approved, int pending)
    {
        var read = Kernel.Json.AwJson.Deserialize<LibraryRootChanged>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("library-roots", scenario)), SindriApiClient.Json).Value;

        Assert.Equal((approved, pending), (read.Approved, read.PendingCount));
    }

    [Theory]
    [InlineData("bad-name", "name")]
    [InlineData("duplicate-name", "name")]
    [InlineData("path-not-absolute", "path")]
    [InlineData("path-too-wide", "path")]
    [InlineData("too-many-vaults", "path")]
    [InlineData("inside-another", "path")]
    [InlineData("bad-deny", "deny")]
    [InlineData("remove-not-vault", "name")]
    public void A_400_names_its_field_and_carries_SND_LIB_001(string scenario, string field)
    {
        var answer = RouteAnswer.Problem("library-roots", scenario);
        var error = SindriApiClient.Refusal(answer.Status, System.Text.Encoding.UTF8.GetBytes(answer.Body))!;

        Assert.Equal((HttpStatusCode.BadRequest, "SND-LIB-001"), (answer.Status, error.Code));
        var member = Assert.Single(error.Extensions!, e => string.Equals(e.Key, "field", StringComparison.Ordinal));
        Assert.Equal(field, member.Value.GetString());
    }

    [Fact]
    public void An_unknown_name_is_a_404_and_a_session_is_a_403()
    {
        Assert.Equal(HttpStatusCode.NotFound, RouteAnswer.Problem("library-roots", "remove-unknown").Status);
        Assert.Equal(HttpStatusCode.Forbidden, RouteAnswer.Problem("library-roots", "session-refused").Status);
    }
}
