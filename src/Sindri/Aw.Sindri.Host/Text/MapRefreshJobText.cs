using System.Globalization;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of a finished map refresh job: the promoted version and what the cleanup settled.</summary>
internal static class MapRefreshJobText
{
    internal static string Render(RefreshContextMapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.Create(CultureInfo.InvariantCulture, $"map v{result.Version} for {result.Repo} promoted · {result.Accepted} kept, {result.Removed} removed, {result.Dropped} dropped (cleanup: {result.Cleanup})\n");
    }
}
