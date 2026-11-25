using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>The toolkits root ($AW_SINDRI_HOME/toolkits): which folders are in it, and the generator that makes a new one.</summary>
public interface IToolkitFolders
{
    /// <summary>Lists the folders under the root that hold a toolkit project (one *.Toolkit.csproj), in name order.</summary>
    /// <returns>The folders; empty when the root doesn't exist.</returns>
    public IReadOnlyList<ToolkitFolder> Folders();

    /// <summary>Runs the toolkit generator (scripts/scaffold/new-toolkit.mjs) into a new folder under the root.</summary>
    /// <param name="name">The toolkit name, lowercase words joined by '-' (the folder's name).</param>
    /// <param name="tool">The first tool's name, snake_case.</param>
    /// <param name="needs">The generator's --needs value (database:state,disk:1g,connection:github), or null for none.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The new folder, or SND-TK-013 with what the generator said.</returns>
    public ValueTask<Result<ToolkitFolder>> Generate(string name, string tool, string? needs, CancellationToken cancellationToken);
}
