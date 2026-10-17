namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ITerminalViewer.Open"/> shows.</summary>
/// <param name="Host">The session to attach to.</param>
/// <param name="AttachArgv">The command that attaches to it, built from checked ids.</param>
/// <param name="Title">The window or tab title ('sindri ITM-…').</param>
/// <param name="Window">How long the viewer has to show it (3 s).</param>
public sealed record ViewerRequest(HostRef Host, IReadOnlyList<string> AttachArgv, string Title, TimeSpan Window)
{
    /// <summary>The repo's absolute path, for a viewer that opens its window there ('{cwd}', Warp's 'cwd'); null opens in the user's home.</summary>
    public string? Cwd { get; init; }
}
