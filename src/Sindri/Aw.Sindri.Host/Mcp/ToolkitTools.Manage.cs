using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The toolkit_create, toolkit_pause and toolkit_resume tools (agent-first PR 16c): their definitions and their calls. The tools are properties, not fields, so the order the two files' static fields start in doesn't matter.</summary>
internal static partial class ToolkitTools
{
    private static Tool CreateTool => new()
    {
        Name = Create,
        Description = "Make a new toolkit: the generator writes a toolkit project with one sample tool under Sindri's toolkits folder, and the toolkit is listed as off. A job; build it next with toolkit_build.",
        InputSchema = ContextTools.Schema(
            new JsonObject
            {
                ["name"] = ContextTools.Property("string", "The toolkit's name: lowercase words joined by '-', each starting with a letter, e.g. github-triage"),
                ["tool"] = ContextTools.Property("string", "The first tool's name: lowercase letters, digits and '_', e.g. list_issues"),
                ["needs"] = ContextTools.Property("string", "What the toolkit needs, comma separated: database:<name>, disk:<size>, connection:<name>, e.g. database:state,connection:github"),
            },
            required: ["name", "tool"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = false },
    };

    private static Tool PauseTool => new()
    {
        Name = Pause,
        Description = "Pause a running toolkit: its process keeps running, its tools leave tools/list and calls to it are refused until toolkit_resume.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
    };

    private static Tool ResumeTool => new()
    {
        Name = Resume,
        Description = "Resume a paused toolkit: its tools are listed and callable again.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
    };

    private static async ValueTask<CallToolResult> CreateOne(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Create, arguments, "name", "tool", "needs");
        var name = McpArguments.RequiredString(Create, arguments, "name", "github-triage");
        var first = McpArguments.RequiredString(Create, arguments, "tool", "list_issues");
        var needs = McpArguments.OptionalString(Create, arguments, "needs");
        if ((refused ?? name.Failed() ?? first.Failed() ?? needs.Failed()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var started = await services.GetRequiredService<IService<CreateToolkitRequest, JobStartedResponse>>().Handle(new CreateToolkitRequest(name.Value, first.Value, needs.Value), ct).ConfigureAwait(false);
        return started.IsError
            ? McpResults.Failure(services, started.Error)
            : Answer(new JsonObject { ["name"] = name.Value, ["jobId"] = started.Value.JobId.Value }, $"creating {name.Value} (job {started.Value.JobId.Value})", _plain);
    }

    private static async ValueTask<CallToolResult> PauseOne(IServiceProvider services, string name, CancellationToken ct)
    {
        var done = await services.GetRequiredService<IService<PauseToolkitRequest, PauseToolkitResponse>>().Handle(new PauseToolkitRequest(name), ct).ConfigureAwait(false);
        return done.IsError ? McpResults.Failure(services, done.Error) : Answer(State(done.Value.Name, done.Value.State), $"{done.Value.Name} is paused", _plain);
    }

    private static async ValueTask<CallToolResult> ResumeOne(IServiceProvider services, string name, CancellationToken ct)
    {
        var done = await services.GetRequiredService<IService<ResumeToolkitRequest, ResumeToolkitResponse>>().Handle(new ResumeToolkitRequest(name), ct).ConfigureAwait(false);
        return done.IsError ? McpResults.Failure(services, done.Error) : Answer(State(done.Value.Name, done.Value.State), $"{done.Value.Name} is running again", _plain);
    }
}
