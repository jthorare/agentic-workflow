namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ILauncher.Launch"/> did: the session started (<see cref="StartedLaunch"/>), or it did not (<see cref="FailedLaunch"/>).</summary>
public abstract record LaunchResult
{
    private protected LaunchResult()
    {
    }
}
