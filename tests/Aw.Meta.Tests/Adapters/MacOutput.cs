namespace Aw.Meta.Tests.Adapters;

/// <summary>What the macOS tools print, for the tests that run the macOS adapters through a fake command runner.</summary>
internal static class MacOutput
{
    internal const string MemSize = "51539607552\n";

    internal const string VmStat =
        "Mach Virtual Memory Statistics: (page size of 16384 bytes)\n"
        + "Pages free:                                   101677.\n"
        + "Pages active:                                1289976.\n"
        + "Pages inactive:                              1269176.\n"
        + "Pages speculative:                             20193.\n";

    internal const string Swap = "total = 5632.00M  used = 4025.50M  free = 1606.50M  (encrypted)\n";

    internal const string Ps =
        "    1     0 06-20:15:23  22512 Ss   /sbin/launchd\n"
        + "45366 84627       00:38   1216 S    sleep 60\n"
        + "45400     1       00:01      0 Z    (defunct)\n";
}
