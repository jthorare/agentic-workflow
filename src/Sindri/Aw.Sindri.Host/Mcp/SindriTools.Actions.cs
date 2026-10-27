using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Host.Endpoints;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

internal static partial class SindriTools
{
    /// <summary>Runs sindri_job.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallJob(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var job = McpArguments.Only(Job, arguments, ["jobId", .. McpArguments.PageNames]) is { } refused
            ? refused
            : McpArguments.RequiredString(Job, arguments, "jobId", "JOB-...").Bind(JobRoute.Job);
        if (job.IsError)
        {
            return McpResults.Failure(services, job.Error);
        }

        var answer = await services.GetRequiredService<IService<GetJobRequest, JobView>>().Handle(new GetJobRequest(job.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Job, FencePlan.Job, page.Value);
    }

    /// <summary>Runs sindri_request: files the request through the command chain, then waits for the views to hold it (API P8 D5).</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallRequest(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = SindriBindings.Request(arguments, (JsonTypeInfo<RequestKind>)services.GetRequiredService<JsonSerializerOptions>().GetTypeInfo(typeof(RequestKind)));
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        // why: a request about no item is about the registration's repo, so without the repo header it has nothing to be about
        if (bound.Value.Item is null && services.GetRequiredService<ICallerContextAccessor>().Current?.Repo is null)
        {
            return McpResults.Failure(services, ContextErrors.McpRepoMissing.With(
                "the call has no X-Sindri-Repo header and names no itemId, so the request has no repo (fix: run 'sindri mcp register' in the work repo, or pass an itemId)"));
        }

        var answer = await services.GetRequiredService<IService<FileRequestRequest, RequestFiledResponse>>().Handle(bound.Value, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return McpResults.Failure(services, answer.Error);
        }

        // why: X-Sindri-After isn't honoured on this endpoint, so the tool reads its own write before it answers; a timeout is not a failure
        var caught = await services.GetRequiredService<IProjectorStatus>().WaitFor(answer.Value.Accepted.Sequence, WaitForViews, ct).ConfigureAwait(false);
        return McpResults.Success(services, answer.Value, McpSummaries.Request(answer.Value, bound.Value.Kind, viewsCaughtUp: !caught.IsError));
    }
}
