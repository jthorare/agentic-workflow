using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The built-in sandbox settings (sandbox spec §9). An approved profile with no 'sandbox:' section is valid and gets these;
/// 'sindri doctor' prints "using the built-in sandbox defaults".
/// </summary>
public static class SandboxDefaults
{
    /// <summary>Per-session memory: 20% of RAM, at most 12 GiB.</summary>
    public const string Memory = "auto";

    /// <summary>Per-session CPU share.</summary>
    public const string Cpu = "50%";

    /// <summary>Processes per session.</summary>
    public const int Tasks = 2048;

    /// <summary>Concurrent sandboxed sessions.</summary>
    public const int Sessions = 3;

    /// <summary>The parent slice's memory cap.</summary>
    public const string Total = "60%";

    /// <summary>Added to a Step's timeout for the wall limit.</summary>
    public const string WallGrace = "10m";

    /// <summary>The size of the $HOME tmpfs.</summary>
    public const string HomeTmpfs = "256MiB";

    /// <summary>The longest single suspension the scope's ceiling allows.</summary>
    public const string ParkMax = "24h";

    /// <summary>The attended wall ceiling: off.</summary>
    public const string AttendedWall = "off";

    /// <summary>The model login source.</summary>
    public const ModelLoginSource Login = ModelLoginSource.OwnerCopy;

    /// <summary>The network mode.</summary>
    public const NetworkMode Network = NetworkMode.Enforce;

    /// <summary>The agent CLI's own sandbox.</summary>
    public const InnerSandbox Inner = InnerSandbox.Auto;

    /// <summary>The GitHub credentials.</summary>
    public const GithubCredentials Github = GithubCredentials.None;
}
