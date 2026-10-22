using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// Reads the 'recipes-tree=' line of the 'build-info' file scripts/sindri-app.sh writes beside the published binary, and asks git in the current folder for 'HEAD:recipes/sindri'.
/// No stamp (a dev run) is no answer; no checkout, or a checkout without the folder, leaves <see cref="RecipeTrees.Head"/> null.
/// </summary>
internal sealed class BuildInfoRecipeBuild : IRecipeBuild
{
    private const string Key = "recipes-tree=";

    public async ValueTask<RecipeTrees?> Read(CancellationToken cancellationToken)
    {
        var stamp = Path.Combine(AppContext.BaseDirectory, "build-info");
        if (!File.Exists(stamp))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(stamp, cancellationToken).ConfigureAwait(false);
        var embedded = lines.FirstOrDefault(static l => l.StartsWith(Key, StringComparison.Ordinal))?[Key.Length..].Trim();
        if (string.IsNullOrEmpty(embedded))
        {
            return null;
        }

        var head = await GitProcess.Run(Environment.CurrentDirectory, ["rev-parse", "--verify", "-q", "HEAD:recipes/sindri"], cancellationToken).ConfigureAwait(false);
        return new RecipeTrees(embedded, head.Exit == 0 && head.Output.Trim() is { Length: > 0 } tree ? tree : null);
    }
}
