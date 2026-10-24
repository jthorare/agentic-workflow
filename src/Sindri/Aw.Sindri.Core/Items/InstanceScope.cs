using Aw.Kernel;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The not-found answers of the instance routes (API spec §3.8). Every caller, a session too, reads every instance.
/// </summary>
public static class InstanceScope
{
    /// <summary>The answer for an instance that doesn't exist: SND-ITEM-404.</summary>
    /// <param name="reference">What the caller asked for (the instance id or address).</param>
    /// <returns>The error.</returns>
    public static Error NotFound(string reference) => SindriErrors.ItemNotFound.With($"No workflow instance {reference} in this repo.");

    /// <summary>The instance read's error as the caller sees it: no such instance (SND-WF-001) becomes <see cref="NotFound"/>; any other error is passed on.</summary>
    /// <param name="error">The error the directory or the runtime answered.</param>
    /// <param name="reference">What the caller asked for.</param>
    /// <returns>The error to answer.</returns>
    public static Error Unknown(Error error, string reference)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, WorkflowErrors.InstanceNotFound.Code, StringComparison.Ordinal) ? NotFound(reference) : error;
    }
}
