using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Core.Resources;

#pragma warning disable MA0182 // the red commit: the next one registers the doctor row
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
        _ = (entries, diskLimits);
        throw new NotSupportedException();
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
#pragma warning restore MA0182
