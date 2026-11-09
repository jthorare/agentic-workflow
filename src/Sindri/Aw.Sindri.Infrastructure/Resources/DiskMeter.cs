using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Measures a disk folder with a bounded walk.</summary>
internal static class DiskMeter
{
    /// <summary>Largest number of files one walk visits.</summary>
    internal const int FileCap = 100_000;

    /// <summary>Measures <paramref name="folder"/> and checks it against <paramref name="limitBytes"/>.</summary>
    /// <param name="name">The disk's name, for the message.</param>
    /// <param name="folder">The disk folder.</param>
    /// <param name="limitBytes">The limit.</param>
    /// <returns>The bytes used, or SND-RES-001 naming used and limit.</returns>
    internal static Result<long> Check(string name, string folder, long limitBytes) => throw new NotSupportedException();
}
