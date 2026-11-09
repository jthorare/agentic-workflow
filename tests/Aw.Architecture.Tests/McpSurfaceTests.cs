using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Architecture.Tests;

/// <summary>
/// The MCP surface (API spec §8, engine spec §4.2): the sindri_* and context_* tools reach queries and the one request
/// command (and, todos excepted, the six todo writes: todo spec D2), so the endpoint can't even attempt a decision, a desk, protected or job action, and it never carries a
/// challenge answer or the launch secret. Read from the Sindri host's catalog, the one table the command chain reads.
/// </summary>
[Trait("Lane", "integration")]
public sealed class McpSurfaceTests
{
    /// <summary>The namespace of the Sindri host's MCP endpoint and tools.</summary>
    public const string McpNamespace = "Aw.Sindri.Host.Mcp";

    // why: agents get every connection verb (connections PR 10a, D2) and write memory (PR13, G8): the connection_* and memory_* tools reach those commands; each is Desk class with SessionOk
    private static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        typeof(FileRequestRequest).FullName!,
        typeof(AddConnectionRequest).FullName!,
        typeof(TestConnectionRequest).FullName!,
        typeof(RemoveConnectionRequest).FullName!,
        typeof(SignInConnectionRequest).FullName!,
        typeof(CreateMemoryRequest).FullName!,
        typeof(WriteMemoryFactRequest).FullName!,
        typeof(ForgetMemoryFactRequest).FullName!,
        // why: todo spec D2 (Ph1b): a todo list can't change anything outside Sindri, so agents get the six todo writes (add, change, done, reopen, remove, restore), all request class; MarkTodoRequest carries the last four
        typeof(AddTodoRequest).FullName!,
        typeof(ChangeTodoRequest).FullName!,
        typeof(MarkTodoRequest).FullName!,
        // why: agent-first spec D2 (design.md, PR 9): any caller may answer a waiting ask_owner question, attributed `session:<id>`, so workflow_answer reaches the AnswerQuestion command
        "Aw.Sindri.Features.AnswerQuestion.AnswerQuestionRequest",   // internal to Features, so named by its full name
    };

    private static readonly string[] _launchSecretMarkers = ["LAUNCH_SECRET", "Launch-Secret", "LaunchSecret"];

    [Fact]
    public void Mcp_tools_reference_only_queries_and_the_request_command()
    {
        var catalog = HostIntrospection.Catalog().Rows;
        var commands = catalog.Select(r => r.CommandType?.FullName).OfType<string>().Where(n => !_allowed.Contains(n)).ToHashSet(StringComparer.Ordinal);
        // why: catalog routes are templates ('/sindri/decisions/{decisionId}'); code that builds one loads only the text before the first '{'
        var protectedPrefixes = catalog.Where(r => r.Class != ActionClass.Request).Select(r => r.Route).OfType<string>()
            .Select(r => r.Split('{')[0])
            .SelectMany(r => new[] { r, r.Replace("/sindri", string.Empty, StringComparison.Ordinal) })
            .Where(r => r.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
        var literals = HostIntrospection.LiteralsIn(HostIntrospection.Host, McpNamespace);

        // the rule must be able to fail: a scanner that finds nothing, or a catalog without the decide command, would pass vacuously
        Assert.Contains(typeof(DecideRequest).FullName!, commands);
        Assert.NotEmpty(protectedPrefixes);
        Assert.Contains(literals, l => l.Literal == "/sindri/mcp");
        Assert.Contains(literals, l => l.Literal == "sindri_request");

        var found = McpTypes()
            .SelectMany(t => t.Dependencies.Where(d => commands.Contains(d.Target.FullName)).Select(d => $"{t.FullName} uses command {d.Target.FullName}"))
            .Concat(literals
                .Where(l => protectedPrefixes.Any(p => l.Literal.StartsWith(p, StringComparison.Ordinal)))
                .Select(l => $"{l.Type} holds the route '{l.Literal}' of a command that isn't a request"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found)
            + $". Fix: the MCP tools call queries and {nameof(FileRequestRequest)} only; a human decides, desks and approves, todos excepted (API spec §8, engine spec §4.2, todo spec D2)");
    }

    [Fact]
    public void Mcp_tools_reference_no_challenge_answer_or_launch_secret()
    {
        var challenges = typeof(ChallengeAnswer).Namespace;

        var found = McpTypes()
            .SelectMany(t => t.Dependencies
                .Where(d => d.Target.Namespace?.FullName == challenges || _launchSecretMarkers.Any(m => d.Target.FullName.Contains(m, StringComparison.OrdinalIgnoreCase)))
                .Select(d => $"{t.FullName} uses {d.Target.FullName}"))
            .Concat(HostIntrospection.LiteralsIn(HostIntrospection.Host, McpNamespace)
                .Where(l => _launchSecretMarkers.Any(m => l.Literal.Contains(m, StringComparison.OrdinalIgnoreCase)))
                .Select(l => $"{l.Type} holds the literal '{l.Literal}'"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found)
            + ". Fix: MCP proves no step report and redeems no challenge; it strips every X-Sindri-* header but the repo (API spec §8.1)");
    }

    private static List<ArchUnitNET.Domain.IType> McpTypes()
    {
        var types = Production.Architecture.Types
            .Where(t => t.Namespace.FullName == McpNamespace || t.Namespace.FullName.StartsWith(McpNamespace + ".", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(types); // a renamed namespace must not turn the rule into a pass
        return types;
    }
}
