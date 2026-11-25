namespace Aw.Sindri.Core.Toolkits;

/// <summary>Runs a command in a toolkit's folder (the build and the toolkit's tests). Tests use a fake, never a real dotnet.</summary>
public interface IToolkitCommands
{
    /// <summary>Runs one command and waits for it.</summary>
    /// <param name="workingDir">The folder to run in.</param>
    /// <param name="argv">The command and its arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code and the output (bounded).</returns>
    public ValueTask<ToolkitCommandResult> Run(string workingDir, IReadOnlyList<string> argv, CancellationToken cancellationToken);
}
