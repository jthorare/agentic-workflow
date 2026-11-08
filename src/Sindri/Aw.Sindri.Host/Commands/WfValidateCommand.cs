namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf validate &lt;file&gt; [--json]': reads a workflow graph file and runs the validator in this process, never
/// starting or contacting serve. Exit 0 when nothing refuses, 1 on findings, 2 on a usage or tool error.
/// </summary>
internal static class WfValidateCommand
{
    /// <summary>Runs the verb.</summary>
    /// <param name="args">The arguments after 'wf validate'.</param>
    /// <param name="services">The container, for the block catalog and the names the validator asks about.</param>
    /// <param name="output">Where the answer goes.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The exit code.</returns>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => throw new NotSupportedException();
}
