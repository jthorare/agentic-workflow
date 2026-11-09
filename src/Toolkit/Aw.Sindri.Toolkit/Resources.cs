namespace Aw.Sindri.Toolkit;

/// <summary>Builders for <see cref="ResourceNeed"/>, passed to Toolkit.Needs: Resources.Database("cache"), Resources.Disk("scratch", "1GiB"), Resources.Connection("github").</summary>
public static class Resources
{
    /// <summary>A SQLite database file.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The declaration.</returns>
    public static ResourceNeed Database(string name) => new(ResourceKind.Database, CheckedName(name), Size: null);

    /// <summary>A folder with a size limit.</summary>
    /// <param name="name">The resource name.</param>
    /// <param name="size">The limit, such as "1GiB".</param>
    /// <returns>The declaration.</returns>
    public static ResourceNeed Disk(string name, string size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"Disk '{name}' needs a size such as \"1GiB\".");
        }

        return new ResourceNeed(ResourceKind.Disk, CheckedName(name), size);
    }

    /// <summary>A named connection.</summary>
    /// <param name="name">The connection name.</param>
    /// <returns>The declaration.</returns>
    public static ResourceNeed Connection(string name) => new(ResourceKind.Connection, CheckedName(name), Size: null);

    /// <summary>Checks a resource or toolkit name: lower-case letters, digits and '-', starting and ending with a letter or digit.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The same name.</returns>
    internal static string CheckedName(string name)
    {
        var valid = !string.IsNullOrEmpty(name)
            && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            && name[0] != '-' && name[^1] != '-';
        return valid
            ? name
            : throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"'{name}' is not a valid name: use lower-case letters, digits and '-', not at either end.");
    }
}
