namespace Aw.Meta.Infrastructure.Files;

/// <summary>Which host adapters apply.</summary>
internal enum HostKind
{
    /// <summary>Linux: /proc.</summary>
    Linux,

    /// <summary>macOS: sysctl, vm_stat, ps.</summary>
    Mac,

    /// <summary>Any other OS: the host ports answer AW-META-010.</summary>
    Unsupported,
}
