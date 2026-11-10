using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Starts a detached process that outlives the caller (setsid, or perl's POSIX::setsid where there is no setsid binary), for the recycle waiter of a lane whose own turn is running the command.</summary>
public interface IWaiterLauncher
{
    /// <summary>Starts the command detached.</summary>
    /// <param name="argv">The program and its arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The process id of the detached process, or AW-META-009 when it could not be started.</returns>
    public ValueTask<Result<int>> Start(IReadOnlyList<string> argv, CancellationToken cancellationToken);
}
