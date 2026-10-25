using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// The input rule of a command (engine spec §6.3): a route's instance is the opaque 'WFI-…' id, never an address (the CLI resolves its address first).
/// It runs after authorization, so a caller who may not send the command learns nothing about valid input (API spec §2.1); a request built in code may carry an address.
/// </summary>
internal sealed class ApplyCommandValidator : IValidator<ApplyCommandRequest>
{
    public Result<Unit> Validate(ApplyCommandRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.FromRoute && !WorkflowInstanceId.TryParse(value.Instance, provider: null, out _)
            ? SindriErrors.InvalidInput.With("The route's instance isn't an instance id (fix: 'WFI-' and 32 lower-case hex digits; resolve an address with GET /workflow/instances?address=)")
            : Unit.Value;
    }
}
