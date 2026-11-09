using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Aw.Sindri.Core.Commands;

namespace Aw.Architecture.Tests;

/// <summary>
/// The protected commands (engine spec §4.2, §6.2 W22; API spec §4.1), after owner decision q93: protected rows are no longer
/// terminal-bound and redeem no challenge, so the rules that said so are gone, and PR 2 of agent-first removed the class itself. What still holds: the app's review verbs are desk
/// verbs, and an MCP type never references the app challenge request.
/// Read from the Sindri host's catalog, the one table the command chain and the API both read.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ProtectedCommandTests
{
    [Fact]
    public void The_app_references_the_app_challenge_route_and_never_the_terminal_one()
    {
        var rows = HostIntrospection.Catalog().Rows;
        var terminal = rows.Single(r => r.Action.Value == "IssueChallenge").Route!;
        var app = rows.Single(r => r.Action.Value == "IssueAppChallenge").Route!;
        var strings = Production.Assemblies.Where(a => IsClient(a.GetName().Name ?? string.Empty)).SelectMany(Strings).ToList();

        Assert.DoesNotContain(strings, s => string.Equals(s, terminal, StringComparison.Ordinal) || string.Equals(s, terminal.Replace("/sindri", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.NotEqual(terminal, app, StringComparer.Ordinal);
        Assert.StartsWith(terminal + "/", app, StringComparison.Ordinal);
    }

    [Fact]
    public void No_MCP_type_references_the_app_challenge_request()
    {
        var request = typeof(Sindri.Features.IssueChallenge.IssueAppChallengeRequest).FullName;

        var found = Production.Architecture.Types
            .Where(t => InMcp(t.Namespace.FullName))
            .SelectMany(t => t.Dependencies.Where(d => d.Target.FullName == request).Select(d => $"{t.FullName} uses {d.Target.FullName}"))
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found) + ". Fix: the MCP endpoint stamps a session, and a session never confirms (setup-once spec §4)");
    }

    // One route, class by verb (since owner q93, 2026-10-23, every review verb is desk): the app may only hold verbs that are desk rows.
    // Every verb the app can put in a review request (the members of its DeskVerb enum, by wire name) must be a catalog row on
    // that route whose class is desk.
    [Fact]
    public void Every_verb_the_app_can_send_on_the_shared_review_route_is_a_non_protected_catalog_row()
    {
        var ui = Production.Assemblies.Single(a => a.GetName().Name == Solution.AssemblyOf("Aw.Sindri.Ui"));
        var desk = ui.GetType("Aw.Sindri.Ui.Api.Context.DeskVerb", throwOnError: true)!;
        var request = ui.GetType("Aw.Sindri.Ui.Api.Context.ReviewDecisionRequest", throwOnError: true)!;
        var verbs = desk.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute), inherit: false).Cast<System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute>().Single().Name)
            .ToList();
        var rows = HostIntrospection.Catalog().Rows.Where(r => string.Equals(r.Route, "/sindri/context/map/review", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(verbs);
        Assert.All(verbs, verb => Assert.Contains(rows, r => string.Equals(r.Variant, verb, StringComparison.Ordinal) && r.Class == ActionClass.Desk));
        // The link: the request the app posts carries its verb as DeskVerb, and no string verb beside it.
        Assert.Equal(desk, request.GetProperty("Verb")!.PropertyType);
        Assert.DoesNotContain(request.GetProperties(), p => p.PropertyType == typeof(string) && p.Name.Contains("Verb", StringComparison.Ordinal));
    }

    /// <summary>The Avalonia app (SindriUi, spec §4a 'UI') and any MCP server: clients that never sit at a terminal.</summary>
    private static bool IsClient(string assembly) =>
        assembly.StartsWith("Aw.Sindri.Ui", StringComparison.Ordinal) || assembly.Contains(".Mcp", StringComparison.Ordinal);

    /// <summary>
    /// The Sindri host's MCP namespace: its types depend on no protected request. The host holds every route, so the
    /// route-string half of this rule can't read its whole assembly; McpSurfaceTests reads the MCP methods' literals.
    /// </summary>
    private static bool InMcp(string ns) =>
        ns == McpSurfaceTests.McpNamespace || ns.StartsWith(McpSurfaceTests.McpNamespace + ".", StringComparison.Ordinal);

    /// <summary>The string literals compiled into <paramref name="assembly"/> (its user-string heap).</summary>
    private static List<string> Strings(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var strings = new List<string>();
        if (metadata.GetHeapSize(HeapIndex.UserString) <= 1)
        {
            return strings;
        }

        for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = metadata.GetNextHandle(handle))
        {
            strings.Add(metadata.GetUserString(handle));
        }

        return strings;
    }
}
