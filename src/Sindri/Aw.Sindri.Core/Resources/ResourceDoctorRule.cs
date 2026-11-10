using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Resources;

/// <summary>The doctor's 'resources' row: how many resources exist, what they hold, and any disk close to its limit.</summary>
public static class ResourceDoctorRule
{
    /// <summary>Fraction of its limit at which a disk is near it.</summary>
    public const double NearLimit = 0.9;

    /// <summary>Builds the row.</summary>
    /// <param name="entries">What is on disk.</param>
    /// <param name="diskLimits">Each disk's limit in bytes, keyed by <see cref="LimitKey"/>; a disk with no entry has no known limit.</param>
    /// <returns>Ok with the count and total size, or Warn naming a disk at 90% of its limit or more.</returns>
    public static DoctorCheck Check(IReadOnlyList<ResourceEntry> entries, IReadOnlyDictionary<string, long> diskLimits)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(diskLimits);
        foreach (var disk in entries.Where(static e => e.Kind == ResourceKind.Disk))
        {
            if (diskLimits.TryGetValue(LimitKey(disk.Owner, disk.Name), out var limit) && limit > 0 && disk.SizeBytes >= limit * NearLimit)
            {
                return new DoctorCheck("resources", CheckState.Warn, $"disk '{disk.Name}' of {disk.Owner} holds {SandboxUnits.FormatSize(disk.SizeBytes)} of {SandboxUnits.FormatSize(limit)}", "delete files from the disk, or raise its size in the declaration");
            }
        }

        if (entries.Count == 0)
        {
            return new DoctorCheck("resources", CheckState.Ok, "no resources provisioned", Fix: null);
        }

        var total = entries.Sum(static e => e.SizeBytes);
        return new DoctorCheck("resources", CheckState.Ok, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{entries.Count} resources, {SandboxUnits.FormatSize(total)} in all"), Fix: null);
    }

    /// <summary>The key a disk's limit is stored under.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="name">The disk's name.</param>
    /// <returns>The key.</returns>
    public static string LimitKey(ResourceOwner owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return $"{owner}/{name}";
    }
}
