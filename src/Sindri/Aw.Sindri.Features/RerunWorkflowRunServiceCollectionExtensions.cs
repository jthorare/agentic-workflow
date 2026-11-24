using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.RerunWorkflowRun;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the re-run from a step slice ('sindri wf rerun', #3204): behind the command chain; the service checks the run, the step and the input.</summary>
internal static class RerunWorkflowRunServiceCollectionExtensions
{
    internal static IServiceCollection AddRerunWorkflowRun(this IServiceCollection services) =>
        services.AddScoped<IValidator<RerunWorkflowRunRequest>, AcceptAllValidator<RerunWorkflowRunRequest>>().AddCommand<RerunWorkflowRunRequest, RerunWorkflowRunResponse, RerunWorkflowRunService>();
}
