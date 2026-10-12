namespace Aw.Sindri.Host.Server;

/// <summary>The exit code as the process's own: <see cref="Environment.ExitCode"/>.</summary>
internal sealed class ProcessExitCode : IExitCode
{
    public int Code => Environment.ExitCode;

    public void Set(int code) => Environment.ExitCode = code;
}
