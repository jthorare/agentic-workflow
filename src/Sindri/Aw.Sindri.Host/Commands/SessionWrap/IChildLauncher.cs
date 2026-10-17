using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Starts the agent under 'session wrap' with the terminal it was given.</summary>
internal interface IChildLauncher
{
    /// <summary>Starts <paramref name="argv"/> with the environment <paramref name="environment"/> on top of the inherited one.</summary>
    /// <param name="argv">The command line.</param>
    /// <param name="environment">The variables wrap sets.</param>
    /// <returns>The running child, or why it could not start.</returns>
    public Result<IWrapChild> Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> environment);
}
