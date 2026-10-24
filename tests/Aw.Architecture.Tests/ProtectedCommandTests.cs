using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Architecture.Tests;

/// <summary>
/// The protected commands (engine spec §4.2, §6.2 W22; API spec §4.1): every 'protected' catalog row is bound to a
/// terminal and its request redeems the server's challenge, and no client that can't sit at a terminal (the Avalonia
/// app, an MCP server) references a protected request or route. Read from the Sindri host's catalog, the one table the
/// command chain and the API both read.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ProtectedCommandTests
{
    /// <summary>
    /// Protected rows that still confirm another way, each with its reason; a stale entry fails, and review keeps new entries out.
    /// </summary>
    private static readonly Dictionary<string, string> _confirmsOtherwise = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void Every_protected_engine_command_requires_the_challenge()
    {
        var rows = HostIntrospection.Catalog().Rows.Where(r => r.Class == ActionClass.Protected).ToList();

        var unbound = rows.Where(r => !r.NeedsTerminal).Select(r => $"{r.Action.Value} isn't terminal-bound").ToList();
        var unchallenged = rows
            .Where(r => r.CommandType is { } type && !Redeems(type) && !_confirmsOtherwise.ContainsKey(r.Action.Value))
            .Select(r => r.Action.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        var stale = _confirmsOtherwise.Keys
            .Where(a => !rows.Exists(r => r.Action.Value == a && r.CommandType is { } type && !Redeems(type)))
            .Order(StringComparer.Ordinal)
            .ToList();
        var exempted = string.Join("; ", _confirmsOtherwise.Select(e => $"{e.Key}: {e.Value}"));

        Assert.True(unbound.Count == 0, string.Join(Environment.NewLine, unbound) + ". Fix: set NeedsTerminal on every protected row (engine spec §6.2)");
        Assert.True(stale.Count == 0, $"stale exemptions: [{string.Join(", ", stale)}]. Fix: remove them from _confirmsOtherwise (exempted: {exempted})");
        Assert.True(
            unchallenged.Count == 0,
            $"protected requests without a {nameof(ChallengeAnswer)}: [{string.Join(", ", unchallenged)}]. "
            + $"Fix: give the request a {nameof(ChallengeAnswer)} it redeems (engine spec §4.2). Exempted: {exempted}");
    }

    [Fact]
    public void No_client_references_a_protected_request_or_route()
    {
        // The three AppConfirm rows are the app's to send (setup-once spec §4 item 7, SO-12); every other protected row stays the terminal's.
        var rows = HostIntrospection.Catalog().Rows.Where(r => r.Class == ActionClass.Protected && !r.AppConfirm).ToList();
        var types = rows.Select(r => r.CommandType?.FullName).OfType<string>().Append(typeof(ChallengeAnswer).FullName).ToHashSet(StringComparer.Ordinal);
        var routes = ProtectedOnlyRoutes(HostIntrospection.Catalog().Rows.Where(r => !r.AppConfirm)).SelectMany(r => new[] { r, r.Replace("/sindri", string.Empty, StringComparison.Ordinal) }).ToHashSet(StringComparer.Ordinal);

        var found = Production.Architecture.Types
            .Where(t => IsClient(t.Assembly.Name) || InMcp(t.Namespace.FullName))
            .SelectMany(t => t.Dependencies.Where(d => types.Contains(d.Target.FullName)).Select(d => $"{t.FullName} uses {d.Target.FullName}"))
            .Concat(Production.Assemblies.Where(a => IsClient(a.GetName().Name ?? string.Empty)).SelectMany(a => Strings(a).Where(routes.Contains).Select(s => $"{a.GetName().Name} holds route '{s}'")))
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found)
            + ". Fix: a protected action is typed at a terminal; the app shows the CLI form to copy (engine spec §4.2, UI spec E-9)");
    }

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

    /// <summary>
    /// The routes only protected rows serve. One route can carry many verbs, class by verb (a desk accept and a protected merge
    /// share POST /context/map/review, and a GET reads it), so a shared route is not banned from the app: what the app may send on
    /// it is pinned by Every_verb_the_app_can_send_on_the_shared_review_route_is_a_non_protected_catalog_row (its closed DeskVerb set;
    /// the type half sees no protected verb here, the app's review body having no ChallengeAnswer).
    /// </summary>
    /// <param name="rows">The catalog's rows.</param>
    /// <returns>The routes no desk or other row serves.</returns>
    internal static IReadOnlyList<string> ProtectedOnlyRoutes(IEnumerable<CommandRow> rows) =>
        [.. rows
            .Where(r => r.Route is not null)
            .GroupBy(r => r.Route!, StringComparer.Ordinal)
            .Where(g => g.All(r => r.Class == ActionClass.Protected))
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)];

    // One route, class by verb (since owner q93, 2026-10-23, every review verb is desk): the app may only hold verbs that are desk rows.
    // Every verb the app can put in a review request (the members of its DeskVerb enum, by wire name) must be a catalog row on
    // that route whose class is not Protected.
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

    [Fact]
    public void The_banned_routes_are_the_protected_only_ones_and_the_shared_review_route_is_not_banned()
    {
        var all = HostIntrospection.Catalog().Rows.ToList();
        var banned = ProtectedOnlyRoutes(all);

        Assert.NotEmpty(banned);
        Assert.DoesNotContain("/sindri/context/map/review", banned, StringComparer.Ordinal);
        Assert.All(banned, route => Assert.All(all.Where(r => string.Equals(r.Route, route, StringComparison.Ordinal)), r => Assert.Equal(ActionClass.Protected, r.Class)));
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

    private static bool Redeems(Type request) =>
        request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.PropertyType == typeof(ChallengeAnswer));

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
