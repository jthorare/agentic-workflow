using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLocations on macOS through lsof (one call, run by <see cref="MacHostReader"/>); the invokers come from lsof's parent field, with the same ancestry walk as the /proc scan.</summary>
/// <param name="reader">Runs lsof.</param>
/// <param name="selfPid">The scanning process's id.</param>
internal sealed class MacProcessLocations(MacHostReader reader, int selfPid) : IProcessLocations
{
    public async ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken)
    {
        var output = await reader.Lsof(cancellationToken).ConfigureAwait(false);
        if (output.IsError)
        {
            return output.Error;
        }

        var found = LsofOutput.Parse(output.Value);
        if (found.Count == 0)
        {
            return MetaErrors.HostUnreadable.With("unavailable on macOS: lsof listed no process");
        }

        // lsof's R field is each process's parent: the scanner and the shells that ran it are never evidence that another command is running
        var invokers = ProcessAncestry.Chain(selfPid, found.Where(e => e.Ppid > 0).ToDictionary(e => e.Location.Pid, e => e.Ppid));
        return Results.Success<IReadOnlyList<ProcessLocation>>([.. found.Select(e => invokers.Contains(e.Location.Pid) ? e.Location with { IsInvoker = true } : e.Location)]);
    }
}
