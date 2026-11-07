using System.Text.Json;
using Aw.Context.Core.Library;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The library status as one small JSON file in the context home, written by temp file and rename at 0600. An unreadable or damaged file reads as no report; a failed write is dropped (the next pass writes it again).</summary>
/// <param name="settings">Where the context home is.</param>
internal sealed class FileLibraryStatus(ContextStoreSettings settings) : ILibraryStatus
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(LibraryStatusJsonContext.Default);

    private readonly string _path = Path.Combine(settings.Home, "library-status.json");

    public IReadOnlyList<LibraryRootReport> Read()
    {
        try
        {
            var parsed = AwJson.Deserialize<LibraryRootReport[]>(File.ReadAllBytes(_path), _options);
            return parsed.IsError ? [] : parsed.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: no file yet, or one that can't be read, is no report
        {
            return [];
        }
    }

    public void Write(IReadOnlyList<LibraryRootReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.WriteAtomically(_path, AwJson.Serialize(reports.ToArray(), _options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: the report is a courtesy for the doctor row; the next pass writes it again
        {
        }
    }
}
