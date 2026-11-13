using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CreateHandoffItem;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ImportTodoNote;
using Aw.Sindri.Features.KeepTodo;
using Aw.Sindri.Features.KeepTodoUndo;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.PickTodoHold;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Features.UndoTodoHold;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Architecture.Tests;

/// <summary>
/// The MCP surface (API spec §8, engine spec §4.2): the sindri_* and context_* tools reach queries, sindri_request, and the Desk + SessionOk writes listed in
/// _allowed (the connection, memory and todo writes, and the workflow writes of issue 2258 Part 1), so the endpoint can't even attempt a decision, any other desk, protected or job action, and it never carries a
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
        typeof(CreateHandoffItemRequest).FullName!,   // sindri_handoff (#2258 PR 12): hands work to this machine's Sindri, the same item POST /sindri/handoff makes; Desk class, a non-launched session may run it
        typeof(AddConnectionRequest).FullName!,
        typeof(TestConnectionRequest).FullName!,
        typeof(RemoveConnectionRequest).FullName!,
        typeof(SignInConnectionRequest).FullName!,
        typeof(CreateMemoryRequest).FullName!,
        typeof(WriteMemoryFactRequest).FullName!,
        typeof(ForgetMemoryFactRequest).FullName!,
        // why: agent parity (G8, agent-first PR 16): agents build, start and stop the toolkits they write (toolkit_build, toolkit_start, toolkit_stop); the build is a job under a heavy slot, a start refuses an unbuilt or changed toolkit (SND-TK-001), and the sandbox contains the process
        "Aw.Sindri.Features.BuildToolkit.BuildToolkitRequest",
        "Aw.Sindri.Features.StartToolkit.StartToolkitRequest",
        "Aw.Sindri.Features.StopToolkit.StopToolkitRequest",
        // why: agent parity (G8, agent-first PR 16c): agents make (a job: the generator), pause and resume the toolkits they write (toolkit_create, toolkit_pause, toolkit_resume); a pause keeps the process inside its sandbox and only refuses calls
        "Aw.Sindri.Features.CreateToolkit.CreateToolkitRequest",
        "Aw.Sindri.Features.PauseToolkit.PauseToolkitRequest",
        "Aw.Sindri.Features.ResumeToolkit.ResumeToolkitRequest",
        // why: agent parity (G8, agent-first PR 14): resources_purge reaches PurgeResources, Desk class with SessionOk; the service still refuses an active owner or a run in flight (SND-RES-003)
        "Aw.Sindri.Features.PurgeResources.PurgeResourcesRequest",   // internal to Features (no public API rows for one slice), so named by its full name
        // why: agents spec 15.8 (A7): agents_start and agents_worktree_remove are listed so the surface is complete, and refused for every session caller (desk class, no SessionOk, SND-AUTH-002): the owner starts agents
        "Aw.Sindri.Features.StartSession.StartSessionRequest",   // internal to Features, so named by its full name
        "Aw.Sindri.Features.RemoveSessionWorktree.RemoveSessionWorktreeRequest",
        // why: todo spec D2 (Ph1b): a todo list can't change anything outside Sindri, so agents get the six todo writes (add, change, done, reopen, remove, restore), all request class; MarkTodoRequest carries the last four
        typeof(AddTodoRequest).FullName!,
        typeof(ChangeTodoRequest).FullName!,
        typeof(MarkTodoRequest).FullName!,
        // why: agent-first spec D2 (design.md, PR 9): any caller may answer a waiting ask_owner question, attributed `session:<id>`, so workflow_answer reaches the AnswerQuestion command
        "Aw.Sindri.Features.AnswerQuestion.AnswerQuestionRequest",   // internal to Features, so named by its full name
        // why: todo spec D2 (rev 5.3, no exceptions): a pick, a hold pick, their Undo and the note import are request class too, and sindri_todo takes them as actions
        typeof(KeepTodoRequest).FullName!,
        typeof(KeepTodoUndoRequest).FullName!,
        typeof(PickTodoHoldRequest).FullName!,
        typeof(UndoTodoHoldRequest).FullName!,
        typeof(ImportTodoNoteRequest).FullName!,
        // why: issue 2258 Part 1 (every verb for agents): an agent session builds, edits, validates, activates, pauses, archives, runs and cancels workflows through the workflow_* tools; each write is desk class and names the launch as its actor (WriteAttributionTests); the requests are internal to Features, so they're named by full name
        "Aw.Sindri.Features.ActivateWorkflow.ActivateWorkflowRequest",
        "Aw.Sindri.Features.ApplyWorkflowOps.ApplyWorkflowOpsRequest",
        "Aw.Sindri.Features.CancelWorkflowRun.CancelWorkflowRunRequest",
        "Aw.Sindri.Features.CreateWorkflow.CreateWorkflowRequest",
        "Aw.Sindri.Features.DeleteWorkflow.DeleteWorkflowRequest",
        "Aw.Sindri.Features.PauseWorkflow.PauseWorkflowRequest",
        "Aw.Sindri.Features.ReplaceWorkflow.ReplaceWorkflowRequest",
        "Aw.Sindri.Features.RunWorkflow.RunWorkflowRequest",
        "Aw.Sindri.Features.ValidateWorkflow.ValidateWorkflowRequest",
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
