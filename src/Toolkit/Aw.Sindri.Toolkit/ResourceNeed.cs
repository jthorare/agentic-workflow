using System.Globalization;

namespace Aw.Sindri.Toolkit;

/// <summary>One resource a toolkit declares with <see cref="Toolkit.Needs"/>.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Name">The resource name: lower-case letters, digits and '-'.</param>
/// <param name="Size">A disk's size limit such as "1GiB"; null for the other kinds.</param>
public sealed record ResourceNeed(ResourceKind Kind, string Name, string? Size)
{
    /// <summary>The environment variable Sindri sets for this resource (PR 14): SINDRI_DB_, SINDRI_DISK_ or SINDRI_CONNECTION_ plus the upper-cased name, '-' as '_'.</summary>
    public string Variable
    {
        get
        {
            var prefix = Kind switch
            {
                ResourceKind.Database => "SINDRI_DB_",
                ResourceKind.Disk => "SINDRI_DISK_",
                ResourceKind.Connection => "SINDRI_CONNECTION_",
                _ => throw new InvalidOperationException($"Unknown resource kind {Kind}."),
            };
            return prefix + Name.ToUpper(CultureInfo.InvariantCulture).Replace('-', '_');
        }
    }
}
