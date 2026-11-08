using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>
    /// The row for a connection whose token could not be refreshed (connections PR 10b): 'connection:&lt;name&gt;', coloured, text through SafeText.Clean,
    /// and the terminal form 'sindri connection add &lt;name&gt; --oauth' to sign in again.
    /// </summary>
    /// <param name="name">The connection.</param>
    /// <param name="reasonCode">Why the refresh failed, as a code (never a token).</param>
    /// <param name="since">When it failed.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForConnection(ConnectionName name, string reasonCode, DateTimeOffset since, ITextScrubber scrubber) => throw new NotSupportedException();
}
