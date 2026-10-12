namespace Aw.Sindri.Host.Server;

/// <summary>Where the serve process records its exit code, so a background service can set it without touching process state in tests.</summary>
internal interface IExitCode
{
    /// <summary>The code recorded so far (0 until something sets it).</summary>
    public int Code { get; }

    /// <summary>Records <paramref name="code"/> as the process's exit code.</summary>
    /// <param name="code">The exit code.</param>
    public void Set(int code);
}
