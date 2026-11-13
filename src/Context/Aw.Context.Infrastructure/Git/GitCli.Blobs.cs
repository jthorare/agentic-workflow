using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    /// <summary>The text of many blobs from one 'git cat-file --batch' process (#3085): the ids go in on stdin, the replies are parsed in order.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="blobIds">Blob shas; an id that is not a sha, or that the repo does not hold as a blob, comes back null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Blob id to text (decoded as UTF-8, as <see cref="Blob"/> does), null when missing, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<IReadOnlyDictionary<string, string?>>> Blobs(string repo, IReadOnlyList<string> blobIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blobIds);
        var texts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var asked = blobIds.Distinct(StringComparer.Ordinal).Where(IsSha).ToList();
        foreach (var id in blobIds)
        {
            texts[id] = null;
        }

        if (asked.Count == 0)
        {
            return Results.Success<IReadOnlyDictionary<string, string?>>(texts);
        }

        var start = CleanGit(["-C", repo, "-c", "core.quotepath=off", "cat-file", "--batch"]);
        start.RedirectStandardInput = true;
        var run = await Execute(start, "cat-file", CopyBytes, cancellationToken, async (stdin, token) =>
        {
            foreach (var id in asked)
            {
                await stdin.WriteAsync((id + "\n").AsMemory(), token).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        if (run.Value.Exit != 0 || !TryParseBatch(run.Value.Stdout, asked, texts))
        {
            return Failed("cat-file", run.Value.Exit);
        }

        return Results.Success<IReadOnlyDictionary<string, string?>>(texts);
    }

    // Replies come in the order the ids went in: '<sha> <type> <size>\n<bytes>\n', or '<id> missing\n' (also 'ambiguous').
    private static bool TryParseBatch(byte[] output, List<string> asked, Dictionary<string, string?> texts)
    {
        var at = 0;
        foreach (var id in asked)
        {
            var end = Array.IndexOf(output, (byte)'\n', at);
            if (end < 0)
            {
                return false;
            }

            var header = Encoding.UTF8.GetString(output, at, end - at).Split(' ');
            at = end + 1;
            if (header.Length != 3)
            {
                continue;   // missing or ambiguous: stays null
            }

            if (!int.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || at + size > output.Length)
            {
                return false;
            }

            if (header[1] == "blob")
            {
                texts[id] = Utf8(output, at, size);
            }

            at += size + 1;   // the bytes and the newline after them
        }

        return true;
    }

    private static string Utf8(byte[] bytes, int at, int size)
    {
        var bom = size >= 3 && bytes[at] == 0xEF && bytes[at + 1] == 0xBB && bytes[at + 2] == 0xBF ? 3 : 0;   // what the StreamReader behind Blob drops
        return Encoding.UTF8.GetString(bytes, at + bom, size - bom);
    }
}
