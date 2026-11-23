using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>
/// The rule catalog on disk (issue 3787): one listing per '&lt;kind&gt;/&lt;language&gt;/*.yaml' record under the rules folder (for example 'engineering/csharp/x.yaml'),
/// with its id, kind, section, text, status and checks. A record that fails to parse or has no id or text is counted as skipped, never an error.
/// </summary>
/// <param name="rulesFolder">The rules folder (<see cref="RuleFolders.Resolve"/>).</param>
internal sealed class FileRuleCatalog(string rulesFolder) : IRuleCatalogPort
{
    /// <inheritdoc />
    public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken)
    {
        _ = rulesFolder;
        throw new NotSupportedException("red: FileRuleCatalog is not built yet");
    }
}
